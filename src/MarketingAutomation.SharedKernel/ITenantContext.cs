namespace MarketingAutomation.SharedKernel;

/// <summary>
/// Ambient tenant for the current request/job. Resolved from auth (API key or JWT claim)
/// by middleware; background workers set it explicitly per unit of work.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    bool HasTenant { get; }
}

public sealed class TenantContext : ITenantContext
{
    private Guid _tenantId;
    public Guid TenantId => HasTenant ? _tenantId : throw new InvalidOperationException("No tenant set for current scope.");
    public bool HasTenant { get; private set; }

    public void Set(Guid tenantId)
    {
        _tenantId = tenantId;
        HasTenant = true;
    }
}
