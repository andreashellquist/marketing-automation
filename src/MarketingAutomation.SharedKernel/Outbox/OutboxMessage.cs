namespace MarketingAutomation.SharedKernel.Outbox;

/// <summary>
/// Transactional outbox row. Written in the same transaction as the state change
/// that caused it; relayed to the message bus by a background processor. Each module
/// owns its own outbox table (in its schema) so the write is always atomic with the
/// module's domain changes.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid TenantId { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}
