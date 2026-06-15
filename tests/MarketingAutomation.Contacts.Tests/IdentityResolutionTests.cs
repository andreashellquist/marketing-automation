using MarketingAutomation.Modules.Contacts.Application;
using MarketingAutomation.Modules.Contacts.Domain;
using MarketingAutomation.Modules.Contacts.Infrastructure;
using MarketingAutomation.SharedKernel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Contacts.Tests;

public class IdentityResolutionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenant = new();

    public IdentityResolutionTests()
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

    private IdentifyContactHandler Handler(ContactsDbContext db) => new(db, _tenant);

    private static IdentifyContactCommand Identify(IdentifierType type, string value, ContactTraits? traits = null) =>
        new([new IdentifierDto(type, value)], traits ?? new ContactTraits());

    [Fact]
    public async Task First_identify_creates_a_contact()
    {
        await using var db = NewContext();
        var dto = await Handler(db).Handle(
            Identify(IdentifierType.Email, "Alice@Example.com", new ContactTraits(FirstName: "Alice")),
            CancellationToken.None);

        Assert.Equal("Alice", dto.FirstName);
        Assert.Single(dto.Identifiers);
        // identifier was normalized to lowercase
        Assert.Equal("alice@example.com", dto.Identifiers[0].Value);
        Assert.Equal(1, await db.Contacts.CountAsync());
    }

    [Fact]
    public async Task Identify_with_known_identifier_updates_same_contact()
    {
        await using (var db = NewContext())
            await Handler(db).Handle(Identify(IdentifierType.Email, "bob@example.com"), CancellationToken.None);

        await using (var db = NewContext())
        {
            var dto = await Handler(db).Handle(
                Identify(IdentifierType.Email, "bob@example.com", new ContactTraits(LastName: "Smith")),
                CancellationToken.None);
            Assert.Equal("Smith", dto.LastName);
        }

        await using var verify = NewContext();
        Assert.Equal(1, await verify.Contacts.CountAsync());
    }

    [Fact]
    public async Task Anonymous_then_known_identifier_merges_into_one_contact()
    {
        // anonymous web session
        await using (var db = NewContext())
            await Handler(db).Handle(Identify(IdentifierType.AnonymousId, "anon-123"), CancellationToken.None);

        // later, same browser identifies with email -> both identifiers presented together
        await using (var db = NewContext())
        {
            var dto = await Handler(db).Handle(new IdentifyContactCommand(
                [new IdentifierDto(IdentifierType.AnonymousId, "anon-123"),
                 new IdentifierDto(IdentifierType.Email, "carol@example.com")],
                new ContactTraits(FirstName: "Carol")), CancellationToken.None);

            Assert.Equal(2, dto.Identifiers.Count);
            Assert.Equal("Carol", dto.FirstName);
        }

        await using var verify = NewContext();
        Assert.Equal(1, await verify.Contacts.IgnoreQueryFilters().CountAsync(c => !c.IsDeleted));
    }

    [Fact]
    public async Task Two_separate_contacts_merge_when_a_shared_identifier_appears()
    {
        await using (var db = NewContext())
            await Handler(db).Handle(Identify(IdentifierType.Email, "dora@example.com"), CancellationToken.None);
        await using (var db = NewContext())
            await Handler(db).Handle(Identify(IdentifierType.Phone, "+46701234567"), CancellationToken.None);

        // a call presenting both ties the two records together
        await using (var db = NewContext())
            await Handler(db).Handle(new IdentifyContactCommand(
                [new IdentifierDto(IdentifierType.Email, "dora@example.com"),
                 new IdentifierDto(IdentifierType.Phone, "+46701234567")],
                new ContactTraits()), CancellationToken.None);

        await using var verify = NewContext();
        var survivors = await verify.Contacts.Include(c => c.Identifiers).ToListAsync();
        Assert.Single(survivors);
        Assert.Equal(2, survivors[0].Identifiers.Count);
    }

    public void Dispose() => _connection.Dispose();
}
