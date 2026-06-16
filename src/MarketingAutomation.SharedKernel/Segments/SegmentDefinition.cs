namespace MarketingAutomation.SharedKernel.Segments;

/// <summary>
/// The segment AST: a recursive group of leaves combined with AND/OR. A leaf targets a
/// standard field, a custom attribute, or a behavioral event. This shape is the shared
/// vocabulary the visual builder, the AI natural-language builder, and the evaluator all
/// speak — stored as JSON on a Segment and passed across module boundaries.
/// </summary>
public sealed class SegmentGroup
{
    /// <summary>"and" or "or".</summary>
    public string Combinator { get; set; } = "and";
    public List<SegmentLeaf> Leaves { get; set; } = [];
    public List<SegmentGroup> Groups { get; set; } = [];

    public bool IsEmpty => Leaves.Count == 0 && Groups.Count == 0;
}

public static class SegmentLeafKind
{
    public const string Field = "field";       // standard profile field
    public const string Attribute = "attribute"; // tenant-defined custom attribute
    public const string Event = "event";       // behavioral event condition
}

public static class SegmentOperator
{
    public const string Eq = "eq";
    public const string Neq = "neq";
    public const string Contains = "contains";
    public const string IsSet = "set";
    public const string IsNotSet = "notset";
}

public sealed class SegmentLeaf
{
    /// <summary>One of <see cref="SegmentLeafKind"/>.</summary>
    public string Kind { get; set; } = SegmentLeafKind.Field;

    /// <summary>Inverts the leaf's result (e.g. "has NOT opened an email").</summary>
    public bool Negate { get; set; }

    // Field / Attribute
    public string? Field { get; set; }
    public string? Op { get; set; }
    public string? Value { get; set; }

    // Event ("did EventName at least MinCount times within WithinDays")
    public string? EventName { get; set; }
    public int MinCount { get; set; } = 1;
    public int WithinDays { get; set; } = 30;
}
