using System.Text.Json;
using MarketingAutomation.Modules.Campaigns.Domain;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MarketingAutomation.Modules.Campaigns.Infrastructure;

public sealed class CampaignsDbContext(DbContextOptions<CampaignsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    protected override string Schema => "campaigns";

    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignContent> CampaignContents => Set<CampaignContent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tagsConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new());

        var tagsComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            v => v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s.GetHashCode())),
            v => v.ToList());

        modelBuilder.Entity<Campaign>(b =>
        {
            b.ToTable("campaigns");
            b.Property(c => c.Name).HasMaxLength(200);
            b.Property(c => c.Timezone).HasMaxLength(64);
            b.Property(c => c.Status).HasConversion<int>();
            b.Property(c => c.Tags).HasConversion(tagsConverter, tagsComparer).HasColumnType("jsonb");
            b.HasOne(c => c.Content).WithOne().HasForeignKey<CampaignContent>(c => c.CampaignId);
            b.HasIndex(c => new { c.Status, c.Channel });
        });

        modelBuilder.Entity<CampaignContent>(b =>
        {
            b.ToTable("campaign_contents");
            b.Property(c => c.SubjectLine).HasMaxLength(500);
            b.Property(c => c.PreviewText).HasMaxLength(500);
            b.Property(c => c.FromName).HasMaxLength(200);
            b.Property(c => c.FromEmail).HasMaxLength(320);
            b.Property(c => c.ReplyTo).HasMaxLength(320);
            b.Property(c => c.SenderId).HasMaxLength(20);
        });

        base.OnModelCreating(modelBuilder);
    }
}
