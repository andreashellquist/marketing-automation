using MarketingAutomation.Modules.Segments.Domain;
using MarketingAutomation.SharedKernel.Segments;

namespace MarketingAutomation.Modules.Segments.Application;

public sealed record SegmentDto(
    Guid Id, string Name, SegmentType Type, SegmentGroup Definition,
    long? CachedCount, DateTimeOffset CreatedAt)
{
    public static SegmentDto From(Segment s) =>
        new(s.Id, s.Name, s.Type, s.Definition, s.CachedCount, s.CreatedAt);
}

public sealed record SegmentSummaryDto(Guid Id, string Name, SegmentType Type, long? CachedCount, DateTimeOffset CreatedAt);

public sealed record SegmentPreviewDto(long Count, IReadOnlyList<SegmentMatch> Sample);
