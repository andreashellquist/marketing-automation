using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Application;

namespace MarketingAutomation.Modules.Campaigns.Domain;

public enum CampaignStatus
{
    Draft = 1,
    Scheduled = 2,
    Running = 3,
    Paused = 4,
    Completed = 5,
    Cancelled = 6,
    Archived = 7,
}

/// <summary>
/// A one-shot send to an audience. The status state machine guards the lifecycle;
/// content lives in a separate sub-resource (<see cref="CampaignContent"/>) so the
/// campaign shell can be created before content exists.
/// </summary>
public sealed class Campaign : TenantEntity
{
    public required string Name { get; set; }
    public CampaignStatus Status { get; private set; } = CampaignStatus.Draft;
    public Channel Channel { get; set; }
    public Guid? SegmentId { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public string Timezone { get; set; } = "UTC";
    public List<string> Tags { get; set; } = [];

    public CampaignContent? Content { get; set; }

    /// <summary>Number of recipients targeted when the send started (audit snapshot).</summary>
    public int? RecipientCount { get; set; }

    // Allowed status transitions. System-only targets (Running/Completed) are reached
    // through the send flow, not the public PATCH endpoint.
    private static readonly Dictionary<CampaignStatus, CampaignStatus[]> Allowed = new()
    {
        [CampaignStatus.Draft] = [CampaignStatus.Scheduled, CampaignStatus.Cancelled, CampaignStatus.Archived],
        [CampaignStatus.Scheduled] = [CampaignStatus.Running, CampaignStatus.Paused, CampaignStatus.Cancelled],
        [CampaignStatus.Running] = [CampaignStatus.Paused, CampaignStatus.Completed, CampaignStatus.Cancelled],
        [CampaignStatus.Paused] = [CampaignStatus.Scheduled, CampaignStatus.Running, CampaignStatus.Cancelled],
        [CampaignStatus.Completed] = [CampaignStatus.Archived],
        [CampaignStatus.Cancelled] = [CampaignStatus.Archived],
        [CampaignStatus.Archived] = [],
    };

    public void TransitionTo(CampaignStatus target)
    {
        if (Status == target) return;
        if (!Allowed[Status].Contains(target))
            throw new DomainConflictException($"Cannot move campaign from {Status} to {target}.");

        if (target == CampaignStatus.Scheduled && !HasSendableContent())
            throw new DomainConflictException("Campaign content must be set before scheduling.");

        Status = target;
    }

    public bool HasSendableContent() => Channel switch
    {
        Channel.Email => Content is not null
            && !string.IsNullOrWhiteSpace(Content.SubjectLine)
            && !string.IsNullOrWhiteSpace(Content.HtmlBody ?? Content.TextBody),
        Channel.Sms => Content is not null && !string.IsNullOrWhiteSpace(Content.SmsBody),
        _ => Content is not null,
    };
}

/// <summary>Channel-specific content for a campaign (email fields and/or SMS fields).</summary>
public sealed class CampaignContent : Entity
{
    public Guid CampaignId { get; set; }

    // Email
    public string? SubjectLine { get; set; }
    public string? PreviewText { get; set; }
    public string? FromName { get; set; }
    public string? FromEmail { get; set; }
    public string? ReplyTo { get; set; }
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }

    // SMS
    public string? SenderId { get; set; }
    public string? SmsBody { get; set; }
    public bool TrackLinks { get; set; }
}
