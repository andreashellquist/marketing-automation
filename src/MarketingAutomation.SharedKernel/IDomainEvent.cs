using MediatR;

namespace MarketingAutomation.SharedKernel;

/// <summary>In-process domain event, dispatched within the same transaction scope.</summary>
public interface IDomainEvent : INotification;

/// <summary>
/// Cross-module integration event. Published via the transactional outbox —
/// never directly to the bus — so state changes and messages are atomic.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    Guid TenantId { get; }
    DateTimeOffset OccurredAt { get; }
}
