using MarketingAutomation.Modules.Platform.Domain;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Platform.Infrastructure;

public class PlatformDbContext(DbContextOptions<PlatformDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    protected override string Schema => "platform";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(b =>
        {
            b.ToTable("tenants");
            b.HasIndex(t => t.Slug).IsUnique();
            b.Property(t => t.Name).HasMaxLength(200);
            b.Property(t => t.Slug).HasMaxLength(100);
        });

        base.OnModelCreating(modelBuilder);
    }
}
