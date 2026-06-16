using System.Text.Json;
using MarketingAutomation.Modules.Segments.Domain;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Persistence;
using MarketingAutomation.SharedKernel.Segments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MarketingAutomation.Modules.Segments.Infrastructure;

public sealed class SegmentsDbContext(DbContextOptions<SegmentsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    protected override string Schema => "segments";

    public DbSet<Segment> Segments => Set<Segment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var defConverter = new ValueConverter<SegmentGroup, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<SegmentGroup>(v, (JsonSerializerOptions?)null) ?? new());

        var defComparer = new ValueComparer<SegmentGroup>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                      == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<SegmentGroup>(
                JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null) ?? new());

        modelBuilder.Entity<Segment>(b =>
        {
            b.ToTable("segments");
            b.Property(s => s.Name).HasMaxLength(200);
            b.Property(s => s.Type).HasConversion<int>();
            b.Property(s => s.Definition).HasConversion(defConverter, defComparer).HasColumnType("jsonb");
        });

        base.OnModelCreating(modelBuilder);
    }
}
