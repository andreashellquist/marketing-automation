using MarketingAutomation.Modules.Campaigns.Domain;
using MarketingAutomation.Modules.Campaigns.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Application;
using MarketingAutomation.SharedKernel.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Campaigns.Application;

public sealed record GetCampaignQuery(Guid Id) : IRequest<CampaignDto>;

public sealed class GetCampaignHandler(CampaignsDbContext db) : IRequestHandler<GetCampaignQuery, CampaignDto>
{
    public async Task<CampaignDto> Handle(GetCampaignQuery request, CancellationToken ct)
    {
        var campaign = await db.Campaigns.Include(c => c.Content).AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException("Campaign", request.Id);
        return CampaignDto.From(campaign);
    }
}

public sealed record ListCampaignsQuery(
    CampaignStatus? Status, Channel? Channel, string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<CampaignSummaryDto>>;

public sealed class ListCampaignsHandler(CampaignsDbContext db)
    : IRequestHandler<ListCampaignsQuery, PagedResult<CampaignSummaryDto>>
{
    public async Task<PagedResult<CampaignSummaryDto>> Handle(ListCampaignsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = db.Campaigns.AsNoTracking();
        if (request.Status is { } status) query = query.Where(c => c.Status == status);
        if (request.Channel is { } channel) query = query.Where(c => c.Channel == channel);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CampaignSummaryDto(
                c.Id, c.Name, c.Status, c.Channel, c.ScheduledAt, c.RecipientCount, c.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<CampaignSummaryDto>(items, page, pageSize, total);
    }
}

// ---- Stats (delivery counts from Messaging via contract) ----------------------------

public sealed record GetCampaignStatsQuery(Guid Id) : IRequest<CampaignStatsDto>;

public sealed record CampaignStatsDto(
    Guid CampaignId, CampaignStatus Status, int? RecipientCount, MessageStats Delivery);

public sealed class GetCampaignStatsHandler(CampaignsDbContext db, IMessageStatsProvider statsProvider)
    : IRequestHandler<GetCampaignStatsQuery, CampaignStatsDto>
{
    public async Task<CampaignStatsDto> Handle(GetCampaignStatsQuery request, CancellationToken ct)
    {
        var campaign = await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException("Campaign", request.Id);

        var delivery = await statsProvider.GetForSourceAsync("campaign", campaign.Id, ct);
        return new CampaignStatsDto(campaign.Id, campaign.Status, campaign.RecipientCount, delivery);
    }
}
