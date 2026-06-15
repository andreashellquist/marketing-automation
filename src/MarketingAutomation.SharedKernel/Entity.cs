namespace MarketingAutomation.SharedKernel;

/// <summary>Base for all persisted entities. Ids are sortable Guid v7.</summary>
public abstract class Entity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<IIntegrationEvent> _integrationEvents = [];

    /// <summary>In-process events, dispatched via MediatR within the same scope.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>Cross-module events, flushed to the transactional outbox on save.</summary>
    public IReadOnlyList<IIntegrationEvent> IntegrationEvents => _integrationEvents;

    public void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RaiseIntegrationEvent(IIntegrationEvent integrationEvent) => _integrationEvents.Add(integrationEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
    public void ClearIntegrationEvents() => _integrationEvents.Clear();
}

/// <summary>Entity scoped to a tenant. Global query filters enforce isolation.</summary>
public abstract class TenantEntity : Entity
{
    public Guid TenantId { get; set; }
}
