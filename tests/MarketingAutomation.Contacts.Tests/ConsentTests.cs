using MarketingAutomation.Modules.Contacts.Application;
using MarketingAutomation.Modules.Contacts.Domain;
using MarketingAutomation.Modules.Contacts.Infrastructure;
using MarketingAutomation.SharedKernel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Contacts.Tests;

public class ConsentTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenant = new();

    public ConsentTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _tenant.Set(Guid.CreateVersion7());
    }

    private ContactsDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>().UseSqlite(_connection).Options;
        var db = new ContactsDbContext(options, _tenant);
        db.Database.EnsureCreated();
        return db;
    }

    private async Task<Guid> SeedContactAsync()
    {
        await using var db = NewContext();
        var dto = await new IdentifyContactHandler(db, _tenant).Handle(
            new IdentifyContactCommand([new IdentifierDto(IdentifierType.Email, "eve@example.com")],
                new ContactTraits(Email: "eve@example.com")), CancellationToken.None);
        return dto.Id;
    }

    [Fact]
    public async Task Revoking_marketing_consent_adds_a_suppression_entry()
    {
        var contactId = await SeedContactAsync();

        await using (var db = NewContext())
            await new UpdateConsentHandler(db).Handle(new UpdateConsentCommand(
                contactId, Channel.Email, ConsentPurpose.Marketing, ConsentStatus.Revoked, "preference-center"),
                CancellationToken.None);

        await using var verify = NewContext();
        var suppression = await verify.SuppressionEntries.SingleAsync();
        Assert.Equal("eve@example.com", suppression.Value);
        Assert.Equal(SuppressionReason.Unsubscribe, suppression.Reason);
    }

    [Fact]
    public async Task Latest_consent_entry_wins_for_current_state()
    {
        var contactId = await SeedContactAsync();

        await using (var db = NewContext())
        {
            var h = new UpdateConsentHandler(db);
            await h.Handle(new UpdateConsentCommand(contactId, Channel.Email, ConsentPurpose.Marketing,
                ConsentStatus.Granted, "signup"), CancellationToken.None);
        }
        await using (var db = NewContext())
        {
            var dto = await new UpdateConsentHandler(db).Handle(new UpdateConsentCommand(
                contactId, Channel.Email, ConsentPurpose.Marketing, ConsentStatus.Revoked, "stop-reply"),
                CancellationToken.None);

            var emailMarketing = dto.Consent.Single(c =>
                c.Channel == Channel.Email && c.Purpose == ConsentPurpose.Marketing);
            Assert.Equal(ConsentStatus.Revoked, emailMarketing.Status);
        }
    }

    public void Dispose() => _connection.Dispose();
}
