using MarketingAutomation.Modules.Templates.Domain;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Templates.Infrastructure;

public sealed class TemplatesDbContext(DbContextOptions<TemplatesDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    protected override string Schema => "templates";

    public DbSet<Template> Templates => Set<Template>();
    public DbSet<BrandKit> BrandKits => Set<BrandKit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Template>(b =>
        {
            b.ToTable("templates");
            b.Property(t => t.Name).HasMaxLength(200);
            b.Property(t => t.Subject).HasMaxLength(500);
            b.Property(t => t.Channel).HasConversion<int>();
            b.HasIndex(t => new { t.Channel, t.Name });
        });

        modelBuilder.Entity<BrandKit>(b =>
        {
            b.ToTable("brand_kits");
            b.Property(k => k.LogoUrl).HasMaxLength(1000);
            b.Property(k => k.PrimaryColor).HasMaxLength(32);
            b.Property(k => k.FontFamily).HasMaxLength(200);
            b.Property(k => k.CompanyAddress).HasMaxLength(500);
        });

        base.OnModelCreating(modelBuilder);
    }
}
