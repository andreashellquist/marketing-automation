using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Journeys.Domain;

public enum JourneyStatus { Draft = 1, Active = 2, Archived = 3 }

/// <summary>What happens when a contact who has already been through the journey qualifies again.</summary>
public enum ReentryPolicy { Never = 1, Always = 2 }

public static class JourneyNodeType
{
    public const string SendMessage = "send";
    public const string Wait = "wait";              // pause for a fixed duration
    public const string WaitForEvent = "wait_event"; // pause until an event, or a timeout
    public const string Split = "split";            // random A/B branch
    public const string Exit = "exit";
}

/// <summary>
/// A versioned automation graph. Editing an Active journey bumps <see cref="Version"/>;
/// in-flight runs keep finishing on the version they started (pinned on the run).
/// </summary>
public sealed class Journey : TenantEntity
{
    public required string Name { get; set; }
    public JourneyStatus Status { get; private set; } = JourneyStatus.Draft;
    public int Version { get; private set; } = 1;
    public ReentryPolicy ReentryPolicy { get; set; } = ReentryPolicy.Never;

    public Channel Channel { get; set; } = Channel.Email;
    public string? StartNodeId { get; set; }
    public List<JourneyNode> Nodes { get; set; } = [];

    public JourneyNode? FindNode(string? id) => id is null ? null : Nodes.FirstOrDefault(n => n.Id == id);

    public void Activate()
    {
        if (StartNodeId is null || FindNode(StartNodeId) is null)
            throw new SharedKernel.Application.DomainConflictException("A journey needs a valid start node before activation.");
        Status = JourneyStatus.Active;
    }

    public void Archive() => Status = JourneyStatus.Archived;

    /// <summary>Editing an active journey produces a new version so running contacts are unaffected.</summary>
    public void BumpVersion() => Version++;
}

/// <summary>
/// One node in the graph. A single shape carries every node type's fields (like the segment
/// AST) so the whole graph serializes as one JSON column.
/// </summary>
public sealed class JourneyNode
{
    public required string Id { get; set; }
    public required string Type { get; set; }

    /// <summary>Default successor (the "A"/"on-event" branch).</summary>
    public string? NextNodeId { get; set; }

    // send
    public string? Subject { get; set; }
    public string? Body { get; set; }

    // wait
    public int WaitSeconds { get; set; }

    // wait_event
    public string? EventName { get; set; }
    public int TimeoutSeconds { get; set; }
    public string? TimeoutNodeId { get; set; }

    // split
    public int SplitPercent { get; set; }
    public string? NextNodeIdB { get; set; }
}
