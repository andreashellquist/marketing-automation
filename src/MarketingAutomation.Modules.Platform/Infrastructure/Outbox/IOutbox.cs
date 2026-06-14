using System.Text.Json;
using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Platform.Infrastructure.Outbox;

/// <summary>
/// Enqueues integration events into the outbox within the caller's active
/// DbContext/transaction. Nothing is published to the bus here.
/// </summary>
public interface IOutbox
{
    void Enqueue(IIntegrationEvent integrationEvent);
}

public sealed class EfOutbox(PlatformDbContext db) : IOutbox
{
    public void Enqueue(IIntegrationEvent integrationEvent)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            TenantId = integrationEvent.TenantId,
            EventType = integrationEvent.GetType().AssemblyQualifiedName
                ?? throw new InvalidOperationException("Event type has no assembly-qualified name."),
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredAt = integrationEvent.OccurredAt,
        });
    }
}
