using FluentValidation;
using MarketingAutomation.Modules.Campaigns.Domain;
using MarketingAutomation.Modules.Campaigns.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Campaigns.Application;

// ---- Create -------------------------------------------------------------------------

public sealed record CreateCampaignCommand(
    string Name, Channel Channel, Guid? SegmentId, DateTimeOffset? ScheduledAt,
    string? Timezone, List<string>? Tags)
    : IRequest<CampaignDto>;

public sealed class CreateCampaignValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
}

public sealed class CreateCampaignHandler(CampaignsDbContext db) : IRequestHandler<CreateCampaignCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(CreateCampaignCommand request, CancellationToken ct)
    {
        var campaign = new Campaign
        {
            Name = request.Name,
            Channel = request.Channel,
            SegmentId = request.SegmentId,
            ScheduledAt = request.ScheduledAt,
            Timezone = request.Timezone ?? "UTC",
            Tags = request.Tags ?? [],
        };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync(ct);
        return CampaignDto.From(campaign);
    }
}

// ---- Update (name / schedule / tags; Draft or Scheduled only) -----------------------

public sealed record UpdateCampaignCommand(
    Guid Id, string Name, DateTimeOffset? ScheduledAt, string? Timezone, List<string>? Tags)
    : IRequest<CampaignDto>;

public sealed class UpdateCampaignHandler(CampaignsDbContext db) : IRequestHandler<UpdateCampaignCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(UpdateCampaignCommand request, CancellationToken ct)
    {
        var campaign = await Load(db, request.Id, ct);
        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Scheduled))
            throw new DomainConflictException($"A {campaign.Status} campaign cannot be edited.");

        campaign.Name = request.Name;
        campaign.ScheduledAt = request.ScheduledAt;
        campaign.Timezone = request.Timezone ?? campaign.Timezone;
        if (request.Tags is not null) campaign.Tags = request.Tags;

        await db.SaveChangesAsync(ct);
        return CampaignDto.From(campaign);
    }

    private static Task<Campaign> Load(CampaignsDbContext db, Guid id, CancellationToken ct) =>
        CampaignLoader.LoadWithContent(db, id, ct);
}

// ---- Set content --------------------------------------------------------------------

public sealed record SetCampaignContentCommand(
    Guid CampaignId,
    string? SubjectLine, string? PreviewText, string? FromName, string? FromEmail, string? ReplyTo,
    string? HtmlBody, string? TextBody, string? SenderId, string? SmsBody, bool TrackLinks)
    : IRequest<CampaignDto>;

public sealed class SetCampaignContentHandler(CampaignsDbContext db)
    : IRequestHandler<SetCampaignContentCommand, CampaignDto>
{
    public async Task<CampaignDto> Handle(SetCampaignContentCommand request, CancellationToken ct)
    {
        var campaign = await CampaignLoader.LoadWithContent(db, request.CampaignId, ct);
        if (campaign.Status is not (CampaignStatus.Draft or CampaignStatus.Scheduled))
            throw new DomainConflictException($"Content of a {campaign.Status} campaign cannot be changed.");

        campaign.Content ??= new CampaignContent { CampaignId = campaign.Id };
        var c = campaign.Content;
        c.SubjectLine = request.SubjectLine;
        c.PreviewText = request.PreviewText;
        c.FromName = request.FromName;
        c.FromEmail = request.FromEmail;
        c.ReplyTo = request.ReplyTo;
        c.HtmlBody = request.HtmlBody;
        c.TextBody = request.TextBody;
        c.SenderId = request.SenderId;
        c.SmsBody = request.SmsBody;
        c.TrackLinks = request.TrackLinks;

        await db.SaveChangesAsync(ct);
        return CampaignDto.From(campaign);
    }
}

// ---- Change status (PATCH) ----------------------------------------------------------

public sealed record ChangeCampaignStatusCommand(Guid Id, CampaignStatus Status) : IRequest<CampaignDto>;

public sealed class ChangeCampaignStatusHandler(CampaignsDbContext db)
    : IRequestHandler<ChangeCampaignStatusCommand, CampaignDto>
{
    private static readonly CampaignStatus[] PublicTargets =
        [CampaignStatus.Scheduled, CampaignStatus.Paused, CampaignStatus.Cancelled, CampaignStatus.Archived];

    public async Task<CampaignDto> Handle(ChangeCampaignStatusCommand request, CancellationToken ct)
    {
        if (!PublicTargets.Contains(request.Status))
            throw new DomainConflictException($"{request.Status} is set by the system, not via this endpoint.");

        var campaign = await CampaignLoader.LoadWithContent(db, request.Id, ct);
        campaign.TransitionTo(request.Status);
        await db.SaveChangesAsync(ct);
        return CampaignDto.From(campaign);
    }
}

// ---- Archive (DELETE -> soft) -------------------------------------------------------

public sealed record ArchiveCampaignCommand(Guid Id) : IRequest;

public sealed class ArchiveCampaignHandler(CampaignsDbContext db) : IRequestHandler<ArchiveCampaignCommand>
{
    public async Task Handle(ArchiveCampaignCommand request, CancellationToken ct)
    {
        var campaign = await CampaignLoader.LoadWithContent(db, request.Id, ct);
        campaign.TransitionTo(CampaignStatus.Archived);
        db.Campaigns.Remove(campaign); // soft delete + Archived status
        await db.SaveChangesAsync(ct);
    }
}

internal static class CampaignLoader
{
    public static async Task<Campaign> LoadWithContent(CampaignsDbContext db, Guid id, CancellationToken ct) =>
        await db.Campaigns.Include(c => c.Content).FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Campaign", id);
}
