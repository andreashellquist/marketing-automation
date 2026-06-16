using MarketingAutomation.Modules.Journeys.Application;
using MarketingAutomation.Modules.Journeys.Domain;
using MarketingAutomation.Modules.Journeys.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Journeys.Tests;

public sealed class FakeMessageSender : IMessageSender
{
    public List<SendRequest> Sent { get; } = [];
    public Task SendAsync(SendRequest request, CancellationToken ct)
    {
        Sent.Add(request);
        return Task.CompletedTask;
    }
}

public class JourneyEngineTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenant = new();
    private readonly FakeMessageSender _sender = new();

    public JourneyEngineTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _tenant.Set(Guid.CreateVersion7());
    }

    private JourneysDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<JourneysDbContext>().UseSqlite(_connection).Options;
        var db = new JourneysDbContext(options, _tenant);
        db.Database.EnsureCreated();
        return db;
    }

    private JourneyRunner Runner(JourneysDbContext db) => new(_sender);

    private static JourneyNode Send(string id, string body, string? next) =>
        new() { Id = id, Type = JourneyNodeType.SendMessage, Body = body, NextNodeId = next };
    private static JourneyNode Exit(string id) => new() { Id = id, Type = JourneyNodeType.Exit };

    private async Task<Guid> CreateActiveJourneyAsync(List<JourneyNode> nodes, string startId,
        ReentryPolicy reentry = ReentryPolicy.Never)
    {
        await using var db = NewContext();
        var dto = await new CreateJourneyHandler(db).Handle(
            new CreateJourneyCommand("Welcome", Channel.Email, reentry, startId, nodes), CancellationToken.None);
        await new ActivateJourneyHandler(db).Handle(new ActivateJourneyCommand(dto.Id), CancellationToken.None);
        return dto.Id;
    }

    private async Task<EnrollmentResultDto> EnrollAsync(Guid journeyId, Guid contactId)
    {
        await using var db = NewContext();
        return await new EnrollContactHandler(db, Runner(db)).Handle(
            new EnrollContactCommand(journeyId, contactId, "user@x.com", null), CancellationToken.None);
    }

    [Fact]
    public async Task Runs_straight_through_send_to_exit()
    {
        var id = await CreateActiveJourneyAsync(
            [Send("n1", "hi", "n2"), Exit("n2")], "n1");

        var result = await EnrollAsync(id, Guid.CreateVersion7());

        Assert.Equal("enrolled", result.Outcome);
        Assert.Equal(JourneyRunStatus.Completed, result.Status);
        Assert.Single(_sender.Sent);
        Assert.StartsWith("journey:", _sender.Sent[0].DedupKey);
    }

    [Fact]
    public async Task Pauses_on_a_wait_then_resumes_on_a_scheduler_tick()
    {
        // send -> wait(0s, immediately due) -> send -> exit
        var id = await CreateActiveJourneyAsync(
        [
            Send("n1", "welcome", "n2"),
            new JourneyNode { Id = "n2", Type = JourneyNodeType.Wait, WaitSeconds = 0, NextNodeId = "n3" },
            Send("n3", "followup", "n4"),
            Exit("n4"),
        ], "n1");

        var enroll = await EnrollAsync(id, Guid.CreateVersion7());
        Assert.Equal(JourneyRunStatus.Waiting, enroll.Status);
        Assert.Single(_sender.Sent); // only the welcome so far

        await using (var db = NewContext())
        {
            var advanced = await new AdvanceDueRunsHandler(db, Runner(db)).Handle(
                new AdvanceDueRunsCommand(), CancellationToken.None);
            Assert.Equal(1, advanced);
        }

        Assert.Equal(2, _sender.Sent.Count); // followup sent after the wait
        await using var verify = NewContext();
        Assert.Equal(JourneyRunStatus.Completed,
            (await verify.JourneyRuns.SingleAsync(r => r.Id == enroll.RunId)).Status);
    }

    [Fact]
    public async Task Waits_for_an_event_and_resumes_when_it_arrives()
    {
        var contactId = Guid.CreateVersion7();
        var id = await CreateActiveJourneyAsync(
        [
            Send("n1", "please confirm", "n2"),
            new JourneyNode { Id = "n2", Type = JourneyNodeType.WaitForEvent, EventName = "confirmed", NextNodeId = "n3" },
            Send("n3", "thanks", "n4"),
            Exit("n4"),
        ], "n1");

        await EnrollAsync(id, contactId);
        Assert.Single(_sender.Sent); // parked on the event

        await using (var db = NewContext())
        {
            var advanced = await new DeliverEventHandler(db, Runner(db)).Handle(
                new DeliverEventCommand(contactId, "confirmed"), CancellationToken.None);
            Assert.Equal(1, advanced);
        }

        Assert.Equal(2, _sender.Sent.Count);
    }

    [Fact]
    public async Task An_unrelated_event_does_not_resume_a_parked_run()
    {
        var contactId = Guid.CreateVersion7();
        var id = await CreateActiveJourneyAsync(
        [
            Send("n1", "please confirm", "n2"),
            new JourneyNode { Id = "n2", Type = JourneyNodeType.WaitForEvent, EventName = "confirmed", NextNodeId = "n3" },
            Send("n3", "thanks", "n4"),
            Exit("n4"),
        ], "n1");

        await EnrollAsync(id, contactId);

        await using var db = NewContext();
        var advanced = await new DeliverEventHandler(db, Runner(db)).Handle(
            new DeliverEventCommand(contactId, "something_else"), CancellationToken.None);
        Assert.Equal(0, advanced);
        Assert.Single(_sender.Sent);
    }

    [Fact]
    public async Task Wait_for_event_timeout_takes_the_timeout_branch()
    {
        var contactId = Guid.CreateVersion7();
        var id = await CreateActiveJourneyAsync(
        [
            Send("n1", "please confirm", "n2"),
            new JourneyNode
            {
                Id = "n2", Type = JourneyNodeType.WaitForEvent, EventName = "confirmed",
                TimeoutSeconds = 3600, NextNodeId = "n3", TimeoutNodeId = "n4",
            },
            Send("n3", "thanks", "exit"),
            Send("n4", "we missed you", "exit"),
            Exit("exit"),
        ], "n1");

        var enroll = await EnrollAsync(id, contactId);

        // Force the timeout to be due, then tick the scheduler.
        await using (var db = NewContext())
        {
            var run = await db.JourneyRuns.SingleAsync(r => r.Id == enroll.RunId);
            run.WakeUpAtTicks = DateTimeOffset.UtcNow.AddMinutes(-1).UtcTicks;
            await db.SaveChangesAsync();
        }
        await using (var db = NewContext())
            await new AdvanceDueRunsHandler(db, Runner(db)).Handle(new AdvanceDueRunsCommand(), CancellationToken.None);

        Assert.Equal(2, _sender.Sent.Count);
        Assert.Equal("we missed you", _sender.Sent[1].Body); // timeout branch, not the on-event branch
    }

    [Fact]
    public async Task Reentry_never_blocks_a_second_enrollment_while_live()
    {
        var contactId = Guid.CreateVersion7();
        var id = await CreateActiveJourneyAsync(
        [
            Send("n1", "hi", "n2"),
            new JourneyNode { Id = "n2", Type = JourneyNodeType.WaitForEvent, EventName = "x", NextNodeId = "n3" },
            Exit("n3"),
        ], "n1", ReentryPolicy.Never);

        await EnrollAsync(id, contactId);
        var second = await EnrollAsync(id, contactId);

        Assert.Equal("already_enrolled", second.Outcome);
        Assert.Single(_sender.Sent);
    }

    public void Dispose() => _connection.Dispose();
}
