using System.Runtime.CompilerServices;
using MarketingAutomation.Modules.Segments.Infrastructure;
using MarketingAutomation.SharedKernel.Contracts;
using MarketingAutomation.SharedKernel.Segments;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Segments.Infrastructure;

/// <summary>
/// Resolves a campaign/journey audience from a segment definition (SharedKernel contract).
/// A null selector means the whole contact base (an empty definition matches all). This is
/// what makes <c>IAudienceResolver</c> finally resolve a real <c>segmentId</c>.
/// </summary>
public sealed class SegmentAudienceResolver(SegmentsDbContext db, ISegmentEvaluator evaluator) : IAudienceResolver
{
    public async IAsyncEnumerable<AudienceMember> ResolveAsync(
        Guid? segmentId, [EnumeratorCancellation] CancellationToken ct)
    {
        var definition = new SegmentGroup();
        if (segmentId is { } id)
        {
            var segment = await db.Segments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (segment is null) yield break;
            definition = segment.Definition;
        }

        await foreach (var match in evaluator.EvaluateAsync(definition, ct))
            yield return new AudienceMember(match.ContactId, match.Email, match.Phone, match.Timezone);
    }
}
