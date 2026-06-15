namespace MarketingAutomation.SharedKernel.Outbox;

/// <summary>
/// Implemented by every module DbContext so a single background processor can drain
/// all module outboxes without referencing the modules directly.
/// </summary>
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxMessage>> FetchPendingAsync(int batchSize, int maxAttempts, CancellationToken ct);
    Task SaveOutboxAsync(CancellationToken ct);
}
