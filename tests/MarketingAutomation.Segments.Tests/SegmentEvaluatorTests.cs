using MarketingAutomation.Modules.Contacts.Domain;
using MarketingAutomation.Modules.Contacts.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Segments;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Segments.Tests;

public sealed class FakeEventAudienceQuery : IEventAudienceQuery
{
    // keyed by event name -> identifier set
    public Dictionary<string, HashSet<string>> Events { get; } = new();

    public Task<HashSet<string>> IdentifiersWithEventAsync(
        string eventName, int withinDays, int minCount, CancellationToken ct) =>
        Task.FromResult(Events.GetValueOrDefault(eventName) ?? new HashSet<string>());
}

public class SegmentEvaluatorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenant = new();
    private readonly FakeEventAudienceQuery _events = new();

    public SegmentEvaluatorTests()
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

    private async Task SeedAsync()
    {
        await using var db = NewContext();
        db.Contacts.Add(new Contact
        {
            Email = "alice@x.com", FirstName = "Alice", Locale = "sv",
            CustomAttributes = new() { ["tier"] = "vip" },
        });
        db.Contacts.Add(new Contact
        {
            Email = "bob@x.com", FirstName = "Bob", Locale = "en",
            CustomAttributes = new() { ["tier"] = "free" },
        });
        await db.SaveChangesAsync();
    }

    private SegmentEvaluator Evaluator(ContactsDbContext db) => new(db, _events);

    private async Task<List<string?>> EmailsAsync(SegmentGroup def)
    {
        await using var db = NewContext();
        var emails = new List<string?>();
        await foreach (var m in Evaluator(db).EvaluateAsync(def, CancellationToken.None))
            emails.Add(m.Email);
        return emails;
    }

    private static SegmentLeaf Field(string field, string op, string? value = null) =>
        new() { Kind = SegmentLeafKind.Field, Field = field, Op = op, Value = value };

    [Fact]
    public async Task Empty_definition_matches_all_contacts()
    {
        await SeedAsync();
        Assert.Equal(2, (await EmailsAsync(new SegmentGroup())).Count);
    }

    [Fact]
    public async Task Field_equality_filters()
    {
        await SeedAsync();
        var def = new SegmentGroup { Leaves = { Field("locale", SegmentOperator.Eq, "sv") } };
        var emails = await EmailsAsync(def);
        Assert.Equal(["alice@x.com"], emails);
    }

    [Fact]
    public async Task Custom_attribute_equality_filters()
    {
        await SeedAsync();
        var def = new SegmentGroup
        {
            Leaves = { new SegmentLeaf { Kind = SegmentLeafKind.Attribute, Field = "tier", Op = SegmentOperator.Eq, Value = "vip" } },
        };
        Assert.Equal(["alice@x.com"], await EmailsAsync(def));
    }

    [Fact]
    public async Task And_requires_all_leaves()
    {
        await SeedAsync();
        var def = new SegmentGroup
        {
            Combinator = "and",
            Leaves = { Field("locale", SegmentOperator.Eq, "sv"), Field("firstName", SegmentOperator.Eq, "Alice") },
        };
        Assert.Single(await EmailsAsync(def));
    }

    [Fact]
    public async Task Or_requires_any_leaf()
    {
        await SeedAsync();
        var def = new SegmentGroup
        {
            Combinator = "or",
            Leaves = { Field("firstName", SegmentOperator.Eq, "Alice"), Field("firstName", SegmentOperator.Eq, "Bob") },
        };
        Assert.Equal(2, (await EmailsAsync(def)).Count);
    }

    [Fact]
    public async Task Event_leaf_matches_identifiers_from_event_query()
    {
        await SeedAsync();
        _events.Events["order.completed"] = ["alice@x.com"];
        var def = new SegmentGroup
        {
            Leaves = { new SegmentLeaf { Kind = SegmentLeafKind.Event, EventName = "order.completed", MinCount = 2, WithinDays = 90 } },
        };
        Assert.Equal(["alice@x.com"], await EmailsAsync(def));
    }

    [Fact]
    public async Task Flagship_bought_twice_but_not_opened_recently()
    {
        await SeedAsync();
        // both bought twice; only alice opened an email recently
        _events.Events["order.completed"] = ["alice@x.com", "bob@x.com"];
        _events.Events["email.opened"] = ["alice@x.com"];

        var def = new SegmentGroup
        {
            Combinator = "and",
            Leaves =
            {
                new SegmentLeaf { Kind = SegmentLeafKind.Event, EventName = "order.completed", MinCount = 2, WithinDays = 90 },
                new SegmentLeaf { Kind = SegmentLeafKind.Event, EventName = "email.opened", WithinDays = 30, Negate = true },
            },
        };

        // bob bought twice and did NOT open -> matches; alice opened -> excluded
        Assert.Equal(["bob@x.com"], await EmailsAsync(def));
    }

    public void Dispose() => _connection.Dispose();
}
