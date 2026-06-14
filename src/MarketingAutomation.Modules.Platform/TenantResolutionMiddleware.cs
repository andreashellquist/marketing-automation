using MarketingAutomation.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace MarketingAutomation.Modules.Platform;

/// <summary>
/// Resolves the current tenant for the request. Placeholder strategy: X-Tenant-Id
/// header. Replaced by API-key / JWT-claim resolution when auth lands — the rest
/// of the system only ever sees <see cref="ITenantContext"/>.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var value)
            && Guid.TryParse(value, out var tenantId))
        {
            ((TenantContext)tenantContext).Set(tenantId);
        }

        await next(context);
    }
}
