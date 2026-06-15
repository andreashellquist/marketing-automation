using MarketingAutomation.SharedKernel.Outbox;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MarketingAutomation.Modules.Platform.Infrastructure.Outbox;

/// <summary>
/// Drains every module's outbox and relays pending messages to the bus. Resolves all
/// registered <see cref="IOutboxStore"/> implementations (one per module DbContext),
/// so new modules are picked up just by registering their context as a store.
/// </summary>
public sealed class OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 100;
    private const int MaxAttempts = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAllStoresAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox processing failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessAllStoresAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var stores = scope.ServiceProvider.GetServices<IOutboxStore>();

        foreach (var store in stores)
        {
            var pending = await store.FetchPendingAsync(BatchSize, MaxAttempts, ct);
            if (pending.Count == 0) continue;

            foreach (var message in pending)
            {
                try
                {
                    var eventType = Type.GetType(message.EventType)
                        ?? throw new InvalidOperationException($"Unknown event type '{message.EventType}'.");
                    var @event = System.Text.Json.JsonSerializer.Deserialize(message.Payload, eventType)
                        ?? throw new InvalidOperationException("Outbox payload deserialized to null.");

                    await publisher.Publish(@event, eventType, ct);
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.AttemptCount++;
                    message.LastError = ex.Message;
                    logger.LogWarning(ex, "Outbox message {MessageId} failed (attempt {Attempt})",
                        message.Id, message.AttemptCount);
                }
            }

            await store.SaveOutboxAsync(ct);
        }
    }
}
