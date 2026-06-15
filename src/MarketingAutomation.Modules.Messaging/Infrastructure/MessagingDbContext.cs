using MarketingAutomation.Modules.Messaging.Domain;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Messaging.Infrastructure;

public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    protected override string Schema => "messaging";

    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Message>(b =>
        {
            b.ToTable("messages");
            b.Property(m => m.Recipient).HasMaxLength(320);
            b.Property(m => m.RecipientTimezone).HasMaxLength(64);
            b.Property(m => m.Subject).HasMaxLength(500);
            b.Property(m => m.FromName).HasMaxLength(200);
            b.Property(m => m.FromAddress).HasMaxLength(320);
            b.Property(m => m.Provider).HasMaxLength(64);
            b.Property(m => m.ProviderMessageId).HasMaxLength(200);
            b.Property(m => m.DedupKey).HasMaxLength(320);
            b.Property(m => m.SourceType).HasMaxLength(64);
            b.Property(m => m.StatusReason).HasMaxLength(500);

            // At-most-once: one message per (tenant, dedup key).
            b.HasIndex(m => new { m.TenantId, m.DedupKey }).IsUnique();
            // DLR lookups by provider message id.
            b.HasIndex(m => new { m.Provider, m.ProviderMessageId });
            // Frequency-cap counting: marketing messages to a recipient over a window.
            b.HasIndex(m => new { m.Recipient, m.Purpose, m.SentAtTicks });
        });

        base.OnModelCreating(modelBuilder);
    }
}
