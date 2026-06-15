using MarketingAutomation.Modules.Campaigns.Application;
using MarketingAutomation.Modules.Campaigns.Domain;
using MarketingAutomation.Modules.Campaigns.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Application;
using MarketingAutomation.SharedKernel.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Campaigns.Tests;

public class CampaignLifecycleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TenantContext _tenant = new();
    private readonly FakeAudienceResolver _audience = new();
    private readonly FakeMessageSender _sender = new();

    public CampaignLifecycleTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _tenant.Set(Guid.CreateVersion7());
    }

    private CampaignsDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CampaignsDbContext>().UseSqlite(_connection).Options;
        var db = new CampaignsDbContext(options, _tenant);
        db.Database.EnsureCreated();
        return db;
    }

    private async Task<Guid> CreateCampaignAsync(Channel channel = Channel.Email)
    {
        await using var db = NewContext();
        var dto = await new CreateCampaignHandler(db).Handle(
            new CreateCampaignCommand("Summer Sale", channel, null, null, "Europe/Stockholm", ["sale"]),
            CancellationToken.None);
        return dto.Id;
    }

    private async Task SetEmailContentAsync(Guid id)
    {
        await using var db = NewContext();
        await new SetCampaignContentHandler(db).Handle(new SetCampaignContentCommand(
            id, "40% off", "This week only", "Shop", "hi@shop.test", null,
            "<h1>Sale</h1>", "Sale", null, null, false), CancellationToken.None);
    }

    [Fact]
    public async Task New_campaign_starts_in_draft()
    {
        var id = await CreateCampaignAsync();
        await using var db = NewContext();
        var dto = await new GetCampaignHandler(db).Handle(new GetCampaignQuery(id), CancellationToken.None);
        Assert.Equal(CampaignStatus.Draft, dto.Status);
        Assert.Contains("sale", dto.Tags);
    }

    [Fact]
    public async Task Cannot_schedule_without_content()
    {
        var id = await CreateCampaignAsync();
        await using var db = NewContext();
        await Assert.ThrowsAsync<DomainConflictException>(() =>
            new ChangeCampaignStatusHandler(db).Handle(
                new ChangeCampaignStatusCommand(id, CampaignStatus.Scheduled), CancellationToken.None));
    }

    [Fact]
    public async Task Can_schedule_once_content_is_set()
    {
        var id = await CreateCampaignAsync();
        await SetEmailContentAsync(id);
        await using var db = NewContext();
        var dto = await new ChangeCampaignStatusHandler(db).Handle(
            new ChangeCampaignStatusCommand(id, CampaignStatus.Scheduled), CancellationToken.None);
        Assert.Equal(CampaignStatus.Scheduled, dto.Status);
    }

    [Fact]
    public async Task Running_cannot_be_set_via_the_public_status_endpoint()
    {
        var id = await CreateCampaignAsync();
        await SetEmailContentAsync(id);
        await using var db = NewContext();
        await Assert.ThrowsAsync<DomainConflictException>(() =>
            new ChangeCampaignStatusHandler(db).Handle(
                new ChangeCampaignStatusCommand(id, CampaignStatus.Running), CancellationToken.None));
    }

    [Fact]
    public async Task Sending_a_campaign_messages_every_audience_member_and_completes()
    {
        var id = await CreateCampaignAsync();
        await SetEmailContentAsync(id);
        _audience.Members.Add(new AudienceMember(Guid.CreateVersion7(), "a@x.com", null, "Europe/Stockholm"));
        _audience.Members.Add(new AudienceMember(Guid.CreateVersion7(), "b@x.com", null, null));
        _audience.Members.Add(new AudienceMember(Guid.CreateVersion7(), null, null, null)); // no email -> skipped

        await using var db = NewContext();
        var dto = await new SendCampaignHandler(db, _audience, _sender).Handle(
            new SendCampaignCommand(id), CancellationToken.None);

        Assert.Equal(CampaignStatus.Completed, dto.Status);
        Assert.Equal(2, dto.RecipientCount);
        Assert.Equal(2, _sender.Sent.Count);
        Assert.All(_sender.Sent, r => Assert.Equal(MessagePurpose.Marketing, r.Purpose));
        Assert.All(_sender.Sent, r => Assert.Equal("campaign", r.SourceType));
        // dedup key ties each send to (campaign, contact)
        Assert.All(_sender.Sent, r => Assert.StartsWith($"campaign:{id}:", r.DedupKey));
    }

    [Fact]
    public async Task Test_send_uses_transactional_purpose_and_limits_to_five()
    {
        var id = await CreateCampaignAsync();
        await SetEmailContentAsync(id);

        await using (var db = NewContext())
            await new SendTestHandler(db, _sender).Handle(
                new SendTestCommand(id, ["qa1@x.com", "qa2@x.com"]), CancellationToken.None);

        Assert.Equal(2, _sender.Sent.Count);
        Assert.All(_sender.Sent, r => Assert.Equal(MessagePurpose.Transactional, r.Purpose));

        await using var db2 = NewContext();
        await Assert.ThrowsAsync<DomainConflictException>(() =>
            new SendTestHandler(db2, _sender).Handle(
                new SendTestCommand(id, ["1", "2", "3", "4", "5", "6"]), CancellationToken.None));
    }

    [Fact]
    public async Task Completed_campaign_cannot_be_edited()
    {
        var id = await CreateCampaignAsync();
        await SetEmailContentAsync(id);
        _audience.Members.Add(new AudienceMember(Guid.CreateVersion7(), "a@x.com", null, null));
        await using (var db = NewContext())
            await new SendCampaignHandler(db, _audience, _sender).Handle(new SendCampaignCommand(id), CancellationToken.None);

        await using var db2 = NewContext();
        await Assert.ThrowsAsync<DomainConflictException>(() =>
            new UpdateCampaignHandler(db2).Handle(
                new UpdateCampaignCommand(id, "Renamed", null, null, null), CancellationToken.None));
    }

    public void Dispose() => _connection.Dispose();
}
