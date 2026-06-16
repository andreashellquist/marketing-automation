using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Segments;

namespace MarketingAutomation.Modules.Segments.Domain;

public enum SegmentType
{
    /// <summary>Membership recomputed from the definition on demand.</summary>
    Dynamic = 1,
    /// <summary>A fixed, manually managed member list (materialization deferred).</summary>
    Static = 2,
}

/// <summary>
/// A reusable audience definition. The <see cref="Definition"/> AST is the single source of
/// truth shared by the visual builder, the AI builder, and the evaluator. Counts are cached
/// for fast listing and refreshed on preview/evaluation.
/// </summary>
public sealed class Segment : TenantEntity
{
    public required string Name { get; set; }
    public SegmentType Type { get; set; } = SegmentType.Dynamic;
    public SegmentGroup Definition { get; set; } = new();

    public long? CachedCount { get; set; }
    public DateTimeOffset? CachedCountAt { get; set; }
}
