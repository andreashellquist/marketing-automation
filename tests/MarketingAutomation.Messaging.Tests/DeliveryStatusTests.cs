using MarketingAutomation.Modules.Messaging.Application;
using MarketingAutomation.Modules.Messaging.Domain;
using MarketingAutomation.Modules.Messaging.Infrastructure;
using MarketingAutomation.SharedKernel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Messaging.Tests;

public class DeliveryStatusTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenant = new();
    private readonly FakeSendingControl _control = new();
    private readonly FakeSuppressionChecker _suppression = new();
    private readonly FakeChannelSender _emailSender = new(Channel.Email);

    public DeliveryStatusTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _tenant.Set(Guid.CreateVersion7());
    }

    private MessagingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<MessagingDbContext>().UseSqlite(_connection).Options;
        var db = new MessagingDbContext(options, _tenant);
        db.Database.EnsureCreated();
        return db;
    }

    private async Task<string> SendAndGetProviderIdAsync()
    {
        await using var db = NewContext();
        var handler = new SendMessageHandler(db, _tenant,
            new SendPolicyGate(_control, _suppression, db), [_emailSender]);
        var result = await handler.Handle(
            new SendMessageCommand(Channel.Email, MessagePurpose.Marketing, "u@x.com", "Hi", "k1"),
            CancellationToken.None);
        var message = await db.Messages.SingleAsync(m => m.Id == result.MessageId);
        return message.ProviderMessageId!;
    }

    [Fact]
    public async Task Delivery_receipt_marks_message_delivered()
    {
        var providerId = await SendAndGetProviderIdAsync();
        await using var db = NewContext();

        var result = await new UpdateDeliveryStatusHandler(db, _tenant).Handle(
            new UpdateDeliveryStatusCommand("fake", providerId, DeliveryStatus.Delivered),
            CancellationToken.None);

        Assert.True(result.Applied);
        Assert.Equal(MessageStatus.Delivered, result.Status);
    }

    [Fact]
    public async Task Duplicate_delivery_receipt_is_ignored()
    {
        var providerId = await SendAndGetProviderIdAsync();

        await using (var db = NewContext())
            await new UpdateDeliveryStatusHandler(db, _tenant).Handle(
                new UpdateDeliveryStatusCommand("fake", providerId, DeliveryStatus.Delivered),
                CancellationToken.None);

        await using (var db = NewContext())
        {
            var second = await new UpdateDeliveryStatusHandler(db, _tenant).Handle(
                new UpdateDeliveryStatusCommand("fake", providerId, DeliveryStatus.Delivered),
                CancellationToken.None);
            Assert.False(second.Applied); // already terminal
        }
    }

    [Fact]
    public async Task Bounce_receipt_marks_bounced_and_raises_event()
    {
        var providerId = await SendAndGetProviderIdAsync();
        await using var db = NewContext();

        var result = await new UpdateDeliveryStatusHandler(db, _tenant).Handle(
            new UpdateDeliveryStatusCommand("fake", providerId, DeliveryStatus.Bounced, IsHardBounce: true, Reason: "no_such_user"),
            CancellationToken.None);

        Assert.Equal(MessageStatus.Bounced, result.Status);
        var pending = await db.FetchPendingAsync(10, 10, CancellationToken.None);
        Assert.Contains(pending, p => p.EventType.Contains("MessageBounced"));
    }

    [Fact]
    public async Task Unknown_provider_message_is_ignored()
    {
        await using var db = NewContext();
        var result = await new UpdateDeliveryStatusHandler(db, _tenant).Handle(
            new UpdateDeliveryStatusCommand("fake", "does-not-exist", DeliveryStatus.Delivered),
            CancellationToken.None);

        Assert.False(result.Applied);
    }

    public void Dispose() => _connection.Dispose();
}
