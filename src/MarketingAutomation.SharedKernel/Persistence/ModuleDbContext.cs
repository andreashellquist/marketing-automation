using System.Linq.Expressions;
using System.Text.Json;
using MarketingAutomation.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.SharedKernel.Persistence;

/// <summary>
/// Base DbContext for every module. Provides, for free:
///   • tenant isolation (global query filter on <see cref="TenantEntity"/>)
///   • soft delete (deletes become <c>IsDeleted = true</c>)
///   • audit stamping (CreatedAt / UpdatedAt)
///   • a transactional outbox flushed atomically with domain changes.
/// Each module supplies its own <see cref="Schema"/> so tables don't collide.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options, ITenantContext tenantContext)
    : DbContext(options), IOutboxStore
{
    protected abstract string Schema { get; }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Current tenant, read fresh on every query. The global filter references this
    /// property *on the context instance* (not the injected service) so EF re-evaluates
    /// it per query rather than baking one tenant into the cached model.
    /// </summary>
    public Guid CurrentTenantId => tenantContext.HasTenant ? tenantContext.TenantId : Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasIndex(m => m.ProcessedAt).HasFilter("\"ProcessedAt\" IS NULL");
            b.Property(m => m.EventType).HasMaxLength(500);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // We always assign Guid v7 ids ourselves, so keys are never store-generated.
            // Without this, EF treats a non-default key reached via a navigation as an
            // existing row (Modified) instead of a new one (Added).
            var idProperty = entityType.FindProperty("Id");
            if (idProperty is { ClrType: var t } && t == typeof(Guid) && idProperty.IsPrimaryKey())
            {
                modelBuilder.Entity(entityType.ClrType).Property("Id").ValueGeneratedNever();
            }

            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(TenantFilter(entityType.ClrType));
                modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(TenantEntity.TenantId));
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    private LambdaExpression TenantFilter(Type clrType)
    {
        var parameter = Expression.Parameter(clrType, "e");
        // Reference CurrentTenantId on *this context instance* — EF parameterizes context
        // member access and evaluates it against the executing context on each query.
        var tenantMatches = Expression.Equal(
            Expression.Property(parameter, nameof(TenantEntity.TenantId)),
            Expression.Property(Expression.Constant(this), nameof(CurrentTenantId)));
        var notDeleted = Expression.Not(Expression.Property(parameter, nameof(Entity.IsDeleted)));
        return Expression.Lambda(Expression.AndAlso(tenantMatches, notDeleted), parameter);
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
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }

        FlushIntegrationEvents();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void FlushIntegrationEvents()
    {
        var entitiesWithEvents = ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.IntegrationEvents.Count > 0)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            foreach (var @event in entity.IntegrationEvents)
            {
                OutboxMessages.Add(new OutboxMessage
                {
                    TenantId = @event.TenantId,
                    EventType = @event.GetType().AssemblyQualifiedName
                        ?? throw new InvalidOperationException("Event type has no assembly-qualified name."),
                    Payload = JsonSerializer.Serialize(@event, @event.GetType()),
                    OccurredAt = @event.OccurredAt,
                });
            }
            entity.ClearIntegrationEvents();
        }
    }

    public async Task<IReadOnlyList<OutboxMessage>> FetchPendingAsync(int batchSize, int maxAttempts, CancellationToken ct) =>
        await OutboxMessages
            .Where(m => m.ProcessedAt == null && m.AttemptCount < maxAttempts)
            .OrderBy(m => m.Id) // Guid v7 is time-ordered, so this preserves OccurredAt order
            .Take(batchSize)
            .ToListAsync(ct);

    public Task SaveOutboxAsync(CancellationToken ct) => base.SaveChangesAsync(ct);
}
