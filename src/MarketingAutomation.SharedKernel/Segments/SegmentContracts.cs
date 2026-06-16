namespace MarketingAutomation.SharedKernel.Segments;

/// <summary>One contact that matches a segment definition.</summary>
public sealed record SegmentMatch(Guid ContactId, string? Email, string? Phone, string? Timezone);

/// <summary>
/// Evaluates a segment AST against the contact base (implemented by Contacts, which holds
/// profile data and pulls behavioral matches from Events via <see cref="IEventAudienceQuery"/>).
/// </summary>
public interface ISegmentEvaluator
{
    Task<long> CountAsync(SegmentGroup definition, CancellationToken ct);
    IAsyncEnumerable<SegmentMatch> EvaluateAsync(SegmentGroup definition, CancellationToken ct);
}

/// <summary>
/// Behavioral lookup for the segment evaluator (implemented by Events). Returns the set of
/// identifier values (e.g. emails) that performed an event at least <paramref name="minCount"/>
/// times within the given window.
/// </summary>
public interface IEventAudienceQuery
{
    Task<HashSet<string>> IdentifiersWithEventAsync(
        string eventName, int withinDays, int minCount, CancellationToken ct);
}

/// <summary>Turns a natural-language description into a segment AST (implemented by the Ai module).</summary>
public interface ISegmentAiBuilder
{
    Task<SegmentGroup> BuildAsync(string description, CancellationToken ct);
}
