using MarketingAutomation.Modules.Campaigns.Domain;
using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Campaigns.Application;

public sealed record CampaignContentDto(
    string? SubjectLine, string? PreviewText, string? FromName, string? FromEmail, string? ReplyTo,
    string? HtmlBody, string? TextBody, string? SenderId, string? SmsBody, bool TrackLinks)
{
    public static CampaignContentDto? From(CampaignContent? c) => c is null ? null : new(
        c.SubjectLine, c.PreviewText, c.FromName, c.FromEmail, c.ReplyTo,
        c.HtmlBody, c.TextBody, c.SenderId, c.SmsBody, c.TrackLinks);
}

public sealed record CampaignDto(
    Guid Id,
    string Name,
    CampaignStatus Status,
    Channel Channel,
    Guid? SegmentId,
    DateTimeOffset? ScheduledAt,
    string Timezone,
    IReadOnlyList<string> Tags,
    int? RecipientCount,
    CampaignContentDto? Content,
    DateTimeOffset CreatedAt)
{
    public static CampaignDto From(Campaign c) => new(
        c.Id, c.Name, c.Status, c.Channel, c.SegmentId, c.ScheduledAt, c.Timezone,
        c.Tags, c.RecipientCount, CampaignContentDto.From(c.Content), c.CreatedAt);
}

public sealed record CampaignSummaryDto(
    Guid Id, string Name, CampaignStatus Status, Channel Channel,
    DateTimeOffset? ScheduledAt, int? RecipientCount, DateTimeOffset CreatedAt);
