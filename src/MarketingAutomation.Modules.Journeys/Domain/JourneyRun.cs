using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Journeys.Domain;

public enum JourneyRunStatus
{
    Active = 1,    // currently advancing
    Waiting = 2,   // paused on a wait / wait-for-event node
    Completed = 3, // reached an exit or ran off the end
}

/// <summary>
/// The durable state of one contact moving through one journey version. Waits are
/// persisted here (<see cref="WakeUpAtTicks"/> / <see cref="WaitEventName"/>) rather than
/// held in memory, so a restart loses nothing — the scheduler re-discovers due runs.
/// </summary>
public sealed class JourneyRun : TenantEntity
{
    public Guid JourneyId { get; set; }
    public int JourneyVersion { get; set; }
    public Guid ContactId { get; set; }

    public required string Recipient { get; set; }
    public string? RecipientTimezone { get; set; }

    public string? CurrentNodeId { get; set; }
    public JourneyRunStatus Status { get; set; } = JourneyRunStatus.Active;

    /// <summary>When a time-wait (or wait-for-event timeout) is due, in UTC ticks. Null when not waiting on time.</summary>
    public long? WakeUpAtTicks { get; set; }

    /// <summary>The event this run is parked on, if it's a wait-for-event node.</summary>
    public string? WaitEventName { get; set; }
}
