using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Messaging.Domain;

public sealed record MessageSent(
    Guid EventId, Guid TenantId, DateTimeOffset OccurredAt,
    Guid MessageId, Channel Channel, Guid? ContactId)
    : IIntegrationEvent;

public sealed record MessageDelivered(
    Guid EventId, Guid TenantId, DateTimeOffset OccurredAt, Guid MessageId)
    : IIntegrationEvent;

/// <summary>
/// Raised on a hard bounce. Consumed (in a later phase) by Contacts to add a
/// suppression entry, closing the deliverability loop.
/// </summary>
public sealed record MessageBounced(
    Guid EventId, Guid TenantId, DateTimeOffset OccurredAt,
    Guid MessageId, Channel Channel, string Recipient, bool IsHard)
    : IIntegrationEvent;
