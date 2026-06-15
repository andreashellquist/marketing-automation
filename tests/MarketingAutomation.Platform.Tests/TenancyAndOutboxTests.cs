using MarketingAutomation.Modules.Platform.Domain;
using MarketingAutomation.Modules.Platform.Infrastructure;
using MarketingAutomation.SharedKernel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Platform.Tests;

public sealed record TestEvent(Guid EventId, Guid TenantId, DateTimeOffset OccurredAt, string Name)
    : IIntegrationEvent;

public class TenancyAndOutboxTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenantContext = new();

    public TenancyAndOutboxTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    private PlatformDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseSqlite(_connection)
            .Options;
        var db = new PlatformDbContext(options, _tenantContext);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task Audit_fields_are_set_on_create_and_update()
    {
        _tenantContext.Set(Guid.CreateVersion7());
        await using var db = CreateContext();

        var tenant = new Modules.Platform.Domain.Tenant { Name = "Acme", Slug = "acme" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        Assert.NotEqual(default, tenant.CreatedAt);
        Assert.Equal(tenant.CreatedAt, tenant.UpdatedAt);

        tenant.Name = "Acme Inc";
        await db.SaveChangesAsync();
        Assert.True(tenant.UpdatedAt > tenant.CreatedAt);
    }

    [Fact]
    public async Task Delete_is_soft_by_convention()
    {
        _tenantContext.Set(Guid.CreateVersion7());
        await using var db = CreateContext();

        var tenant = new Modules.Platform.Domain.Tenant { Name = "Acme", Slug = "acme" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync();

        var raw = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenant.Id);
        Assert.True(raw.IsDeleted);
    }

    [Fact]
    public async Task Raised_integration_event_is_flushed_to_outbox_atomically()
    {
        var tenantId = Guid.CreateVersion7();
        _tenantContext.Set(tenantId);
        await using var db = CreateContext();

        var tenant = new Tenant { Name = "Acme", Slug = "acme" };
        tenant.RaiseIntegrationEvent(new TestEvent(Guid.CreateVersion7(), tenantId, DateTimeOffset.UtcNow, "hello"));
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var message = await db.OutboxMessages.SingleAsync();
        Assert.Equal(tenantId, message.TenantId);
        Assert.Null(message.ProcessedAt);
        Assert.Contains("hello", message.Payload);
        Assert.Contains(nameof(TestEvent), message.EventType);
        // event list is cleared after flush so a second save won't duplicate it
        Assert.Empty(tenant.IntegrationEvents);
    }

    [Fact]
    public async Task Outbox_store_fetches_only_unprocessed_messages()
    {
        var tenantId = Guid.CreateVersion7();
        _tenantContext.Set(tenantId);
        await using var db = CreateContext();

        var tenant = new Tenant { Name = "Acme", Slug = "acme" };
        tenant.RaiseIntegrationEvent(new TestEvent(Guid.CreateVersion7(), tenantId, DateTimeOffset.UtcNow, "first"));
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var pending = await db.FetchPendingAsync(10, 10, CancellationToken.None);
        Assert.Single(pending);

        pending[0].ProcessedAt = DateTimeOffset.UtcNow;
        await db.SaveOutboxAsync(CancellationToken.None);

        Assert.Empty(await db.FetchPendingAsync(10, 10, CancellationToken.None));
    }

    [Fact]
    public void TenantContext_throws_when_unset()
    {
        var context = new TenantContext();
        Assert.False(context.HasTenant);
        Assert.Throws<InvalidOperationException>(() => context.TenantId);
    }

    public void Dispose() => _connection.Dispose();
}
