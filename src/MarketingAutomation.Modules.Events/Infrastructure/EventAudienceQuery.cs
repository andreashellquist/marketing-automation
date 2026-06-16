using MarketingAutomation.SharedKernel.Segments;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Events.Infrastructure;

/// <summary>
/// Behavioral lookup for the segment evaluator (SharedKernel contract). Returns identifier
/// values that performed an event at least N times within a window — the building block for
/// event-based segment leaves like "bought twice in 90 days".
/// </summary>
public sealed class EventAudienceQuery(EventsDbContext db) : IEventAudienceQuery
{
    public async Task<HashSet<string>> IdentifiersWithEventAsync(
        string eventName, int withinDays, int minCount, CancellationToken ct)
    {
        var sinceTicks = DateTimeOffset.UtcNow.AddDays(-withinDays).UtcTicks;

        var matches = await db.Events
            .Where(e => e.Name == eventName && e.OccurredAtTicks >= sinceTicks)
            .GroupBy(e => e.IdentifierValue)
            .Select(g => new { Identifier = g.Key, Count = g.Count() })
            .Where(x => x.Count >= minCount)
            .Select(x => x.Identifier)
            .ToListAsync(ct);

        return matches.ToHashSet();
    }
}
