using MarketingAutomation.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Platform.Infrastructure;

/// <summary>Exposes a tenant's sending policy to the Messaging pipeline (via the SharedKernel contract).</summary>
public sealed class SendingControl(PlatformDbContext db) : ISendingControl
{
    public async Task<TenantSendingPolicy> GetPolicyAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        return tenant is null
            ? TenantSendingPolicy.Default
            : new TenantSendingPolicy(
                tenant.SendingPaused,
                tenant.MaxMarketingPerDay,
                tenant.QuietHoursEnabled,
                tenant.QuietStartHour,
                tenant.QuietEndHour);
    }
}
