namespace MarketingAutomation.SharedKernel;

/// <summary>Base for all persisted entities. Ids are sortable Guid v7.</summary>
public abstract class Entity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;
    public void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>Entity scoped to a tenant. Global query filters enforce isolation.</summary>
public abstract class TenantEntity : Entity
{
    public Guid TenantId { get; set; }
}
