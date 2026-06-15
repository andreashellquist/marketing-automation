using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Contacts.Domain;

public sealed record ContactIdentified(
    Guid EventId, Guid TenantId, DateTimeOffset OccurredAt, Guid ContactId, bool IsNew)
    : IIntegrationEvent;

public sealed record ConsentChanged(
    Guid EventId, Guid TenantId, DateTimeOffset OccurredAt,
    Guid ContactId, Channel Channel, ConsentPurpose Purpose, ConsentStatus Status)
    : IIntegrationEvent;

public sealed record ContactSuppressed(
    Guid EventId, Guid TenantId, DateTimeOffset OccurredAt,
    Channel Channel, string Value, SuppressionReason Reason)
    : IIntegrationEvent;
