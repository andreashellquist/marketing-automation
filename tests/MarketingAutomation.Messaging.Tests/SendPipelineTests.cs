using MarketingAutomation.Modules.Messaging.Application;
using MarketingAutomation.Modules.Messaging.Domain;
using MarketingAutomation.Modules.Messaging.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Messaging.Tests;

public class SendPipelineTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenant = new();
    private readonly FakeSendingControl _control = new();
    private readonly FakeSuppressionChecker _suppression = new();
    private readonly FakeChannelSender _emailSender = new(Channel.Email);

    public SendPipelineTests()
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

    private SendMessageHandler Handler(MessagingDbContext db) =>
        new(db, _tenant, new SendPolicyGate(_control, _suppression, db), [_emailSender]);

    private static SendMessageCommand Send(string dedupKey, MessagePurpose purpose = MessagePurpose.Marketing,
        string recipient = "user@x.com", string? tz = null) =>
        new(Channel.Email, purpose, recipient, "Hello", dedupKey, Subject: "Hi", RecipientTimezone: tz);

    [Fact]
    public async Task Allowed_message_is_sent_and_raises_an_event()
    {
        await using var db = NewContext();
        var result = await Handler(db).Handle(Send("k1"), CancellationToken.None);

        Assert.Equal(MessageStatus.Sent, result.Status);
        Assert.Equal(1, _emailSender.SendCount);
        var pending = await db.FetchPendingAsync(10, 10, CancellationToken.None);
        Assert.Contains(pending, p => p.EventType.Contains("MessageSent"));
    }

    [Fact]
    public async Task Suppressed_recipient_is_blocked_and_not_sent()
    {
        _suppression.Suppressed.Add("user@x.com");
        await using var db = NewContext();
        var result = await Handler(db).Handle(Send("k1"), CancellationToken.None);

        Assert.Equal(MessageStatus.Suppressed, result.Status);
        Assert.Equal("suppressed", result.Reason);
        Assert.Equal(0, _emailSender.SendCount);
    }

    [Fact]
    public async Task Kill_switch_holds_the_message()
    {
        _control.Policy = TenantSendingPolicy.Default with { SendingPaused = true };
        await using var db = NewContext();
        var result = await Handler(db).Handle(Send("k1"), CancellationToken.None);

        Assert.Equal(MessageStatus.Held, result.Status);
        Assert.Equal("sending_paused", result.Reason);
        Assert.Equal(0, _emailSender.SendCount);
    }

    [Fact]
    public async Task Quiet_hours_hold_marketing_in_recipient_timezone()
    {
        // 06:00 UTC is 07:00 in Stockholm (CET, winter) -> inside a 21->08 quiet window.
        _control.Policy = TenantSendingPolicy.Default with
        {
            QuietHoursEnabled = true, QuietStartHour = 21, QuietEndHour = 8,
        };
        await using var db = NewContext();

        // Drive the gate directly to control "now".
        var gate = new SendPolicyGate(_control, _suppression, db);
        var morning = new DateTimeOffset(2026, 1, 15, 6, 0, 0, TimeSpan.Zero);
        var decision = await gate.EvaluateAsync(_tenant.TenantId, Channel.Email, MessagePurpose.Marketing,
            "user@x.com", "Europe/Stockholm", morning, CancellationToken.None);

        Assert.Equal(PolicyOutcome.Hold, decision.Outcome);
        Assert.Equal("quiet_hours", decision.Reason);
    }

    [Fact]
    public async Task Frequency_cap_suppresses_once_exceeded()
    {
        _control.Policy = TenantSendingPolicy.Default with { MaxMarketingPerDay = 2 };
        await using var db = NewContext();
        var handler = Handler(db);

        Assert.Equal(MessageStatus.Sent, (await handler.Handle(Send("k1"), CancellationToken.None)).Status);
        Assert.Equal(MessageStatus.Sent, (await handler.Handle(Send("k2"), CancellationToken.None)).Status);

        var third = await handler.Handle(Send("k3"), CancellationToken.None);
        Assert.Equal(MessageStatus.Suppressed, third.Status);
        Assert.Equal("frequency_capped", third.Reason);
    }

    [Fact]
    public async Task Transactional_messages_bypass_suppression_and_cap()
    {
        _suppression.Suppressed.Add("user@x.com");
        _control.Policy = TenantSendingPolicy.Default with { MaxMarketingPerDay = 0 };
        await using var db = NewContext();

        var result = await Handler(db).Handle(
            Send("k1", MessagePurpose.Transactional), CancellationToken.None);

        Assert.Equal(MessageStatus.Sent, result.Status);
    }

    [Fact]
    public async Task Same_dedup_key_sends_only_once()
    {
        await using var db = NewContext();
        var handler = Handler(db);

        var first = await handler.Handle(Send("dup"), CancellationToken.None);
        var second = await handler.Handle(Send("dup"), CancellationToken.None);

        Assert.Equal(first.MessageId, second.MessageId);
        Assert.Equal(1, _emailSender.SendCount);
        Assert.Equal(1, await db.Messages.CountAsync());
    }

    [Fact]
    public async Task Failed_provider_send_marks_message_failed()
    {
        _emailSender.ShouldFail = true;
        await using var db = NewContext();
        var result = await Handler(db).Handle(Send("k1"), CancellationToken.None);

        Assert.Equal(MessageStatus.Failed, result.Status);
    }

    public void Dispose() => _connection.Dispose();
}
