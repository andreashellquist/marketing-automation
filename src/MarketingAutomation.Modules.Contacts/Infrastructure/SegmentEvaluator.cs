using System.Runtime.CompilerServices;
using System.Text.Json;
using MarketingAutomation.Modules.Contacts.Domain;
using MarketingAutomation.SharedKernel.Segments;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Contacts.Infrastructure;

/// <summary>
/// Evaluates a segment AST against the contact base (SharedKernel contract). v1 evaluates
/// in memory over the tenant's contacts so custom-attribute and event leaves are fully
/// supported on any provider; pushing field/attribute predicates down to SQL is a later
/// optimization. Event leaves are resolved once up front via <see cref="IEventAudienceQuery"/>.
/// </summary>
public sealed class SegmentEvaluator(ContactsDbContext db, IEventAudienceQuery eventQuery) : ISegmentEvaluator
{
    public async Task<long> CountAsync(SegmentGroup definition, CancellationToken ct)
    {
        long count = 0;
        await foreach (var _ in EvaluateAsync(definition, ct)) count++;
        return count;
    }

    public async IAsyncEnumerable<SegmentMatch> EvaluateAsync(
        SegmentGroup definition, [EnumeratorCancellation] CancellationToken ct)
    {
        var eventSets = await ResolveEventSetsAsync(definition, ct);

        var contacts = db.Contacts.AsNoTracking().OrderBy(c => c.Id).AsAsyncEnumerable();
        await foreach (var contact in contacts.WithCancellation(ct))
        {
            if (EvalGroup(contact, definition, eventSets))
                yield return new SegmentMatch(contact.Id, contact.Email, contact.Phone, contact.Timezone);
        }
    }

    private async Task<Dictionary<string, HashSet<string>>> ResolveEventSetsAsync(
        SegmentGroup root, CancellationToken ct)
    {
        var sets = new Dictionary<string, HashSet<string>>();
        foreach (var leaf in CollectEventLeaves(root))
        {
            var key = EventKey(leaf);
            if (sets.ContainsKey(key)) continue;
            sets[key] = await eventQuery.IdentifiersWithEventAsync(
                leaf.EventName!, leaf.WithinDays, leaf.MinCount, ct);
        }
        return sets;
    }

    private static IEnumerable<SegmentLeaf> CollectEventLeaves(SegmentGroup group)
    {
        foreach (var leaf in group.Leaves)
            if (leaf.Kind == SegmentLeafKind.Event && leaf.EventName is not null)
                yield return leaf;
        foreach (var sub in group.Groups)
            foreach (var leaf in CollectEventLeaves(sub))
                yield return leaf;
    }

    private static string EventKey(SegmentLeaf leaf) => $"{leaf.EventName}|{leaf.WithinDays}|{leaf.MinCount}";

    private static bool EvalGroup(Contact c, SegmentGroup group, Dictionary<string, HashSet<string>> eventSets)
    {
        if (group.IsEmpty) return true; // match-all

        var results = group.Leaves.Select(l => EvalLeaf(c, l, eventSets))
            .Concat(group.Groups.Select(g => EvalGroup(c, g, eventSets)));

        return group.Combinator.Equals("or", StringComparison.OrdinalIgnoreCase)
            ? results.Any(r => r)
            : results.All(r => r);
    }

    private static bool EvalLeaf(Contact c, SegmentLeaf leaf, Dictionary<string, HashSet<string>> eventSets)
    {
        var result = leaf.Kind switch
        {
            SegmentLeafKind.Field => Compare(FieldValue(c, leaf.Field), leaf.Op, leaf.Value),
            SegmentLeafKind.Attribute => Compare(AttributeValue(c, leaf.Field), leaf.Op, leaf.Value),
            SegmentLeafKind.Event => MatchesEvent(c, eventSets.GetValueOrDefault(EventKey(leaf))),
            _ => false,
        };
        return leaf.Negate ? !result : result;
    }

    private static bool MatchesEvent(Contact c, HashSet<string>? identifiers) =>
        identifiers is not null &&
        ((c.Email is not null && identifiers.Contains(c.Email)) ||
         (c.Phone is not null && identifiers.Contains(c.Phone)));

    private static string? FieldValue(Contact c, string? field) => field?.ToLowerInvariant() switch
    {
        "email" => c.Email,
        "phone" => c.Phone,
        "firstname" => c.FirstName,
        "lastname" => c.LastName,
        "locale" => c.Locale,
        "timezone" => c.Timezone,
        _ => null,
    };

    private static string? AttributeValue(Contact c, string? field)
    {
        if (field is null || !c.CustomAttributes.TryGetValue(field, out var value) || value is null)
            return null;
        return value is JsonElement el
            ? (el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString())
            : value.ToString();
    }

    private static bool Compare(string? actual, string? op, string? expected) => op switch
    {
        SegmentOperator.Eq => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
        SegmentOperator.Neq => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
        SegmentOperator.Contains => actual is not null &&
            actual.Contains(expected ?? "", StringComparison.OrdinalIgnoreCase),
        SegmentOperator.IsSet => !string.IsNullOrEmpty(actual),
        SegmentOperator.IsNotSet => string.IsNullOrEmpty(actual),
        _ => false,
    };
}
