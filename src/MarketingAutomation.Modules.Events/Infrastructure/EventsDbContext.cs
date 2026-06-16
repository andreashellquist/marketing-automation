using System.Text.Json;
using MarketingAutomation.Modules.Events.Domain;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MarketingAutomation.Modules.Events.Infrastructure;

public sealed class EventsDbContext(DbContextOptions<EventsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    protected override string Schema => "events";

    public DbSet<StoredEvent> Events => Set<StoredEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var converter = new ValueConverter<Dictionary<string, object?>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, object?>>(v, (JsonSerializerOptions?)null) ?? new());

        var comparer = new ValueComparer<Dictionary<string, object?>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                      == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<Dictionary<string, object?>>(
                JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null) ?? new());

        modelBuilder.Entity<StoredEvent>(b =>
        {
            b.ToTable("stored_events");
            b.Property(e => e.Name).HasMaxLength(200);
            b.Property(e => e.IdentifierValue).HasMaxLength(320);
            b.Property(e => e.MessageId).HasMaxLength(200);
            b.Property(e => e.Properties).HasConversion(converter, comparer).HasColumnType("jsonb");
            // Idempotency: a (tenant, messageId) pair is ingested at most once.
            b.HasIndex(e => new { e.TenantId, e.MessageId })
                .IsUnique()
                .HasFilter("\"MessageId\" IS NOT NULL");
            // Common access path: a person's events by recency.
            b.HasIndex(e => new { e.IdentifierType, e.IdentifierValue, e.OccurredAtTicks });
            // Segment event windows group by name over a time window.
            b.HasIndex(e => new { e.Name, e.OccurredAtTicks });
        });

        base.OnModelCreating(modelBuilder);
    }
}
