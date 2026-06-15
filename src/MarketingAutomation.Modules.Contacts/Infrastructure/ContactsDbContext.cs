using System.Text.Json;
using MarketingAutomation.Modules.Contacts.Domain;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MarketingAutomation.Modules.Contacts.Infrastructure;

public sealed class ContactsDbContext(DbContextOptions<ContactsDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext)
{
    protected override string Schema => "contacts";

    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<ContactIdentifier> ContactIdentifiers => Set<ContactIdentifier>();
    public DbSet<ConsentEntry> ConsentEntries => Set<ConsentEntry>();
    public DbSet<SuppressionEntry> SuppressionEntries => Set<SuppressionEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var attributesConverter = new ValueConverter<Dictionary<string, object?>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, object?>>(v, (JsonSerializerOptions?)null) ?? new());

        var attributesComparer = new ValueComparer<Dictionary<string, object?>>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null)
                      == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<Dictionary<string, object?>>(
                JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null) ?? new());

        modelBuilder.Entity<Contact>(b =>
        {
            b.ToTable("contacts");
            b.Property(c => c.Email).HasMaxLength(320);
            b.Property(c => c.Phone).HasMaxLength(32);
            b.Property(c => c.FirstName).HasMaxLength(200);
            b.Property(c => c.LastName).HasMaxLength(200);
            b.Property(c => c.Locale).HasMaxLength(20);
            b.Property(c => c.Timezone).HasMaxLength(64);
            b.Property(c => c.CustomAttributes)
                .HasConversion(attributesConverter, attributesComparer)
                .HasColumnType("jsonb");
            b.HasMany(c => c.Identifiers).WithOne().HasForeignKey(i => i.ContactId);
            b.HasMany(c => c.ConsentEntries).WithOne().HasForeignKey(c => c.ContactId);
            b.HasIndex(c => c.Email);
        });

        modelBuilder.Entity<ContactIdentifier>(b =>
        {
            b.ToTable("contact_identifiers");
            b.Property(i => i.Value).HasMaxLength(320);
            // Deterministic resolution: a (type, value) pair maps to at most one contact per
            // tenant. Filtered so soft-deleted rows (e.g. after a merge) free their slot.
            b.HasIndex(i => new { i.TenantId, i.Type, i.Value })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = FALSE");
        });

        modelBuilder.Entity<ConsentEntry>(b =>
        {
            b.ToTable("consent_entries");
            b.Property(c => c.Source).HasMaxLength(200);
            b.Property(c => c.IpAddress).HasMaxLength(64);
            b.HasIndex(c => new { c.ContactId, c.Channel, c.Purpose });
        });

        modelBuilder.Entity<SuppressionEntry>(b =>
        {
            b.ToTable("suppression_entries");
            b.Property(s => s.Value).HasMaxLength(320);
            b.HasIndex(s => new { s.Channel, s.Value }).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
