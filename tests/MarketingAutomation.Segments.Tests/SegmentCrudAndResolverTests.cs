using MarketingAutomation.Modules.Contacts.Domain;
using MarketingAutomation.Modules.Contacts.Infrastructure;
using MarketingAutomation.Modules.Segments.Application;
using MarketingAutomation.Modules.Segments.Domain;
using MarketingAutomation.Modules.Segments.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Segments;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Segments.Tests;

public class SegmentCrudAndResolverTests : IDisposable
{
    private readonly SqliteConnection _segmentsConn;
    private readonly SqliteConnection _contactsConn;
    private readonly TenantContext _tenant = new();
    private readonly FakeEventAudienceQuery _events = new();

    public SegmentCrudAndResolverTests()
    {
        _segmentsConn = new SqliteConnection("DataSource=:memory:");
        _segmentsConn.Open();
        _contactsConn = new SqliteConnection("DataSource=:memory:");
        _contactsConn.Open();
        _tenant.Set(Guid.CreateVersion7());
    }

    private SegmentsDbContext NewSegments()
    {
        var options = new DbContextOptionsBuilder<SegmentsDbContext>().UseSqlite(_segmentsConn).Options;
        var db = new SegmentsDbContext(options, _tenant);
        db.Database.EnsureCreated();
        return db;
    }

    private ContactsDbContext NewContacts()
    {
        var options = new DbContextOptionsBuilder<ContactsDbContext>().UseSqlite(_contactsConn).Options;
        var db = new ContactsDbContext(options, _tenant);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Create_then_get_round_trips_the_definition()
    {
        var def = new SegmentGroup
        {
            Leaves = { new SegmentLeaf { Kind = SegmentLeafKind.Field, Field = "locale", Op = SegmentOperator.Eq, Value = "sv" } },
        };

        Guid id;
        await using (var db = NewSegments())
            id = (await new CreateSegmentHandler(db).Handle(
                new CreateSegmentCommand("Swedes", SegmentType.Dynamic, def), CancellationToken.None)).Id;

        await using (var db = NewSegments())
        {
            var dto = await new GetSegmentHandler(db).Handle(new GetSegmentQuery(id), CancellationToken.None);
            Assert.Equal("Swedes", dto.Name);
            Assert.Single(dto.Definition.Leaves);
            Assert.Equal("locale", dto.Definition.Leaves[0].Field);
        }
    }

    [Fact]
    public async Task Audience_resolver_streams_segment_matches()
    {
        // seed contacts
        await using (var c = NewContacts())
        {
            c.Contacts.Add(new Contact { Email = "sv@x.com", Locale = "sv" });
            c.Contacts.Add(new Contact { Email = "en@x.com", Locale = "en" });
            await c.SaveChangesAsync();
        }

        // create a segment for locale=sv
        Guid segmentId;
        await using (var db = NewSegments())
            segmentId = (await new CreateSegmentHandler(db).Handle(new CreateSegmentCommand(
                "Swedes", SegmentType.Dynamic,
                new SegmentGroup { Leaves = { new SegmentLeaf { Kind = SegmentLeafKind.Field, Field = "locale", Op = SegmentOperator.Eq, Value = "sv" } } }),
                CancellationToken.None)).Id;

        await using var segments = NewSegments();
        await using var contacts = NewContacts();
        var resolver = new SegmentAudienceResolver(segments, new SegmentEvaluator(contacts, _events));

        var emails = new List<string?>();
        await foreach (var member in resolver.ResolveAsync(segmentId, CancellationToken.None))
            emails.Add(member.Email);

        Assert.Equal(["sv@x.com"], emails);
    }

    [Fact]
    public async Task Null_segment_resolves_the_whole_contact_base()
    {
        await using (var c = NewContacts())
        {
            c.Contacts.Add(new Contact { Email = "a@x.com" });
            c.Contacts.Add(new Contact { Email = "b@x.com" });
            await c.SaveChangesAsync();
        }

        await using var segments = NewSegments();
        await using var contacts = NewContacts();
        var resolver = new SegmentAudienceResolver(segments, new SegmentEvaluator(contacts, _events));

        var count = 0;
        await foreach (var _ in resolver.ResolveAsync(null, CancellationToken.None)) count++;
        Assert.Equal(2, count);
    }

    public void Dispose()
    {
        _segmentsConn.Dispose();
        _contactsConn.Dispose();
    }
}
