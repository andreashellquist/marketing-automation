using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Events.Domain;

/// <summary>
/// An immutable behavioral event in the event store. Keyed to a person by identifier
/// (resolution to a canonical contact happens downstream, keeping this module free of
/// any Contacts dependency). <see cref="MessageId"/> makes ingestion idempotent.
/// </summary>
public sealed class StoredEvent : TenantEntity
{
    public required string Name { get; set; }
    public IdentifierType IdentifierType { get; set; }
    public required string IdentifierValue { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>UTC ticks mirror of <see cref="OccurredAt"/> for provider-portable range
    /// queries (segment event windows), since SQLite can't range-query DateTimeOffset.</summary>
    public long OccurredAtTicks { get; set; }
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>Client-supplied dedup key; unique per tenant when present.</summary>
    public string? MessageId { get; set; }
}

public sealed record EventIngested(
    Guid EventId, Guid TenantId, DateTimeOffset OccurredAt,
    Guid StoredEventId, string Name, IdentifierType IdentifierType, string IdentifierValue)
    : IIntegrationEvent;
