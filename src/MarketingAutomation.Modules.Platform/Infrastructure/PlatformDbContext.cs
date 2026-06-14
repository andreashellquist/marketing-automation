using MarketingAutomation.Modules.Platform.Domain;
using MarketingAutomation.Modules.Platform.Infrastructure.Outbox;
using MarketingAutomation.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Platform.Infrastructure;

public class PlatformDbContext(DbContextOptions<PlatformDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("platform");

        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasIndex(t => t.Slug).IsUnique();
            b.Property(t => t.Name).HasMaxLength(200);
            b.Property(t => t.Slug).HasMaxLength(100);
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.HasIndex(m => m.ProcessedAt).HasFilter("\"ProcessedAt\" IS NULL");
            b.Property(m => m.EventType).HasMaxLength(500);
        });

        // Tenant isolation + soft delete on every TenantEntity in this module.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(TenantFilter(entityType.ClrType));
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex(nameof(TenantEntity.TenantId));
            }
        }
    }

    private System.Linq.Expressions.LambdaExpression TenantFilter(Type clrType)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");
        var tenantMatches = System.Linq.Expressions.Expression.Equal(
            System.Linq.Expressions.Expression.Property(parameter, nameof(TenantEntity.TenantId)),
            System.Linq.Expressions.Expression.Property(
                System.Linq.Expressions.Expression.Constant(tenantContext),
                nameof(ITenantContext.TenantId)));
        var notDeleted = System.Linq.Expressions.Expression.IsFalse(
            System.Linq.Expressions.Expression.Property(parameter, nameof(Entity.IsDeleted)));
        return System.Linq.Expressions.Expression.Lambda(
            System.Linq.Expressions.Expression.AndAlso(tenantMatches, notDeleted), parameter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    if (entry.Entity is TenantEntity { TenantId: var tid } tenantEntity
                        && tid == Guid.Empty && tenantContext.HasTenant)
                    {
                        tenantEntity.TenantId = tenantContext.TenantId;
                    }
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Deleted:
                    // Soft delete by convention; hard deletes must be explicit raw SQL.
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
