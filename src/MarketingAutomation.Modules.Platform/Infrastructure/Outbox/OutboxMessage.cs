namespace MarketingAutomation.Modules.Platform.Infrastructure.Outbox;

/// <summary>
/// Transactional outbox row. Written in the same transaction as the state change
/// that caused it; relayed to the message bus by <see cref="OutboxProcessor"/>.
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
