using System.Text.Json;
using MarketingAutomation.Modules.Journeys.Domain;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MarketingAutomation.Modules.Journeys.Infrastructure;

public sealed class JourneysDbContext(DbContextOptions<JourneysDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    protected override string Schema => "journeys";

    public DbSet<Journey> Journeys => Set<Journey>();
    public DbSet<JourneyRun> JourneyRuns => Set<JourneyRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var nodesConverter = new ValueConverter<List<JourneyNode>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<JourneyNode>>(v, (JsonSerializerOptions?)null) ?? new());

        var nodesComparer = new ValueComparer<List<JourneyNode>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                      == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<List<JourneyNode>>(
                JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null) ?? new());

        modelBuilder.Entity<Journey>(b =>
        {
            b.ToTable("journeys");
            b.Property(j => j.Name).HasMaxLength(200);
            b.Property(j => j.Status).HasConversion<int>();
            b.Property(j => j.ReentryPolicy).HasConversion<int>();
            b.Property(j => j.StartNodeId).HasMaxLength(100);
            b.Property(j => j.Nodes).HasConversion(nodesConverter, nodesComparer).HasColumnType("jsonb");
        });

        modelBuilder.Entity<JourneyRun>(b =>
        {
            b.ToTable("journey_runs");
            b.Property(r => r.Recipient).HasMaxLength(320);
            b.Property(r => r.RecipientTimezone).HasMaxLength(64);
            b.Property(r => r.CurrentNodeId).HasMaxLength(100);
            b.Property(r => r.WaitEventName).HasMaxLength(200);
            b.Property(r => r.Status).HasConversion<int>();
            // Scheduler scans due time-waits.
            b.HasIndex(r => new { r.Status, r.WakeUpAtTicks });
            // Event delivery finds runs parked on an event for a contact.
            b.HasIndex(r => new { r.ContactId, r.WaitEventName });
            // Re-entry checks for an existing live run.
            b.HasIndex(r => new { r.JourneyId, r.ContactId, r.Status });
        });

        base.OnModelCreating(modelBuilder);
    }
}
