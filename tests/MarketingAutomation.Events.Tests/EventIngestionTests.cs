using MarketingAutomation.Modules.Events.Application;
using MarketingAutomation.Modules.Events.Infrastructure;
using MarketingAutomation.SharedKernel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Events.Tests;

public class EventIngestionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenant = new();

    public EventIngestionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _tenant.Set(Guid.CreateVersion7());
    }

    private EventsDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<EventsDbContext>().UseSqlite(_connection).Options;
        var db = new EventsDbContext(options, _tenant);
        db.Database.EnsureCreated();
        return db;
    }

    private IngestEventsHandler Handler(EventsDbContext db) => new(db, _tenant);

    private static EventInput Event(string name, string email, string? messageId = null) =>
        new(name, IdentifierType.Email, email, MessageId: messageId);

    [Fact]
    public async Task Ingests_events_and_stores_them()
    {
        await using var db = NewContext();
        var result = await Handler(db).Handle(
            new IngestEventsCommand([Event("page_viewed", "a@x.com"), Event("added_to_cart", "a@x.com")]),
            CancellationToken.None);

        Assert.Equal(2, result.Accepted);
        Assert.Equal(2, await db.Events.CountAsync());
    }

    [Fact]
    public async Task Duplicate_message_ids_within_a_batch_are_dropped()
    {
        await using var db = NewContext();
        var result = await Handler(db).Handle(new IngestEventsCommand(
        [
            Event("order_completed", "b@x.com", "msg-1"),
            Event("order_completed", "b@x.com", "msg-1"),
        ]), CancellationToken.None);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.Duplicates);
        Assert.Equal(1, await db.Events.CountAsync());
    }

    [Fact]
    public async Task Message_id_already_in_store_is_not_ingested_again()
    {
        await using (var db = NewContext())
            await Handler(db).Handle(new IngestEventsCommand([Event("signup", "c@x.com", "evt-99")]),
                CancellationToken.None);

        await using (var db = NewContext())
        {
            var result = await Handler(db).Handle(
                new IngestEventsCommand([Event("signup", "c@x.com", "evt-99")]), CancellationToken.None);
            Assert.Equal(0, result.Accepted);
            Assert.Equal(1, result.Duplicates);
        }

        await using var verify = NewContext();
        Assert.Equal(1, await verify.Events.CountAsync());
    }

    [Fact]
    public async Task Events_without_message_id_are_always_accepted()
    {
        await using var db = NewContext();
        var result = await Handler(db).Handle(new IngestEventsCommand(
            [Event("ping", "d@x.com"), Event("ping", "d@x.com")]), CancellationToken.None);

        Assert.Equal(2, result.Accepted);
    }

    [Fact]
    public async Task Stored_event_raises_an_integration_event_to_the_outbox()
    {
        await using var db = NewContext();
        await Handler(db).Handle(new IngestEventsCommand([Event("checkout", "e@x.com", "m-1")]),
            CancellationToken.None);

        var pending = await db.FetchPendingAsync(10, 10, CancellationToken.None);
        Assert.Single(pending);
        Assert.Contains("EventIngested", pending[0].EventType);
    }

    public void Dispose() => _connection.Dispose();
}
