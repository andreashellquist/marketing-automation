using MarketingAutomation.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MarketingAutomation.Modules.Platform.Infrastructure;

/// <summary>
/// Design-time factory for `dotnet ef` tooling. The runtime DbContext is built by DI
/// with a real <see cref="ITenantContext"/>; migrations never run tenant filters, so an
/// empty context is sufficient here.
/// </summary>
public sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql("Host=localhost;Database=marketing_automation;Username=postgres;Password=devpassword")
            .Options;
        return new PlatformDbContext(options, new TenantContext());
    }
}
