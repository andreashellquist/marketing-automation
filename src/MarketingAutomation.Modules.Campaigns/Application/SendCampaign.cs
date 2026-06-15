using MarketingAutomation.Modules.Campaigns.Domain;
using MarketingAutomation.Modules.Campaigns.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Application;
using MarketingAutomation.SharedKernel.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Campaigns.Application;

/// <summary>
/// Executes a campaign: snapshots the audience, sends one message per member through the
/// Messaging pipeline (which applies the policy gate and idempotency), and completes.
/// The dedup key (campaign + contact) makes re-runs safe — a retried send never doubles up.
/// In production this is invoked by the scheduler at <c>ScheduledAt</c>; the endpoint
/// exposes it for manual/now sends.
/// </summary>
public sealed record SendCampaignCommand(Guid Id) : IRequest<CampaignDto>;

public sealed class SendCampaignHandler(
    CampaignsDbContext db,
    IAudienceResolver audienceResolver,
    IMessageSender messageSender)
    : IRequestHandler<SendCampaignCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(SendCampaignCommand request, CancellationToken ct)
    {
        var campaign = await db.Campaigns.Include(c => c.Content)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException("Campaign", request.Id);

        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Scheduled or CampaignStatus.Paused))
            throw new DomainConflictException($"A {campaign.Status} campaign cannot be sent.");
        if (!campaign.HasSendableContent())
            throw new DomainConflictException("Campaign content is incomplete.");

        // Move Draft/Paused → Scheduled → Running (state machine forbids Draft→Running directly).
        if (campaign.Status != CampaignStatus.Scheduled) campaign.TransitionTo(CampaignStatus.Scheduled);
        campaign.TransitionTo(CampaignStatus.Running);
        await db.SaveChangesAsync(ct);

        var content = campaign.Content!;
        var recipientCount = 0;

        await foreach (var member in audienceResolver.ResolveAsync(campaign.SegmentId, ct))
        {
            var recipient = campaign.Channel == Channel.Email ? member.Email : member.Phone;
            if (string.IsNullOrWhiteSpace(recipient)) continue;

            await messageSender.SendAsync(new SendRequest(
                Channel: campaign.Channel,
                Purpose: MessagePurpose.Marketing,
                Recipient: recipient,
                Body: campaign.Channel == Channel.Email ? (content.HtmlBody ?? content.TextBody ?? "") : content.SmsBody ?? "",
                DedupKey: $"campaign:{campaign.Id}:{member.ContactId}",
                Subject: content.SubjectLine,
                RecipientTimezone: member.Timezone,
                ContactId: member.ContactId,
                FromName: content.FromName,
                FromAddress: content.FromEmail,
                SourceType: "campaign",
                SourceId: campaign.Id), ct);

            recipientCount++;
        }

        campaign.RecipientCount = recipientCount;
        campaign.TransitionTo(CampaignStatus.Completed);
        await db.SaveChangesAsync(ct);

        return CampaignDto.From(campaign);
    }
}

// ---- Send test (up to 5 addresses, transactional so policy doesn't block QA) ---------

public sealed record SendTestCommand(Guid Id, IReadOnlyList<string> Recipients) : IRequest;

public sealed class SendTestHandler(CampaignsDbContext db, IMessageSender messageSender)
    : IRequestHandler<SendTestCommand>
{
    public async Task Handle(SendTestCommand request, CancellationToken ct)
    {
        if (request.Recipients.Count is 0 or > 5)
            throw new DomainConflictException("A test send targets between 1 and 5 recipients.");

        var campaign = await db.Campaigns.Include(c => c.Content).AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException("Campaign", request.Id);
        if (!campaign.HasSendableContent())
            throw new DomainConflictException("Campaign content is incomplete.");

        var content = campaign.Content!;
        foreach (var recipient in request.Recipients)
        {
            await messageSender.SendAsync(new SendRequest(
                Channel: campaign.Channel,
                Purpose: MessagePurpose.Transactional, // QA sends bypass marketing policy
                Recipient: recipient,
                Body: campaign.Channel == Channel.Email ? (content.HtmlBody ?? content.TextBody ?? "") : content.SmsBody ?? "",
                DedupKey: $"campaign-test:{campaign.Id}:{recipient}:{Guid.CreateVersion7()}",
                Subject: content.SubjectLine is null ? null : $"[TEST] {content.SubjectLine}",
                FromName: content.FromName,
                FromAddress: content.FromEmail,
                SourceType: "campaign-test",
                SourceId: campaign.Id), ct);
        }
    }
}
