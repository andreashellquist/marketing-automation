using MarketingAutomation.Modules.Campaigns.Application;
using MarketingAutomation.Modules.Campaigns.Domain;
using MarketingAutomation.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MarketingAutomation.Modules.Campaigns.Endpoints;

public static class CampaignsEndpoints
{
    public static IEndpointRouteBuilder MapCampaignsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/campaigns").WithTags("Campaigns");

        group.MapGet("/", async (CampaignStatus? status, Channel? channel, string? search,
            int? page, int? pageSize, ISender sender) =>
            Results.Ok(await sender.Send(new ListCampaignsQuery(
                status, channel, search, page ?? 1, pageSize ?? 20))));

        group.MapPost("/", async (CreateCampaignCommand body, ISender sender) =>
        {
            var dto = await sender.Send(body);
            return Results.Created($"/api/v1/campaigns/{dto.Id}", dto);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
            Results.Ok(await sender.Send(new GetCampaignQuery(id))));

        group.MapPut("/{id:guid}", async (Guid id, UpdateCampaignBody body, ISender sender) =>
            Results.Ok(await sender.Send(new UpdateCampaignCommand(
                id, body.Name, body.ScheduledAt, body.Timezone, body.Tags))));

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new ArchiveCampaignCommand(id));
            return Results.NoContent();
        });

        group.MapPatch("/{id:guid}/status", async (Guid id, ChangeStatusBody body, ISender sender) =>
            Results.Ok(await sender.Send(new ChangeCampaignStatusCommand(id, body.Status))));

        group.MapPut("/{id:guid}/content", async (Guid id, ContentBody body, ISender sender) =>
            Results.Ok(await sender.Send(new SetCampaignContentCommand(
                id, body.SubjectLine, body.PreviewText, body.FromName, body.FromEmail, body.ReplyTo,
                body.HtmlBody, body.TextBody, body.SenderId, body.SmsBody, body.TrackLinks))));

        group.MapGet("/{id:guid}/stats", async (Guid id, ISender sender) =>
            Results.Ok(await sender.Send(new GetCampaignStatsQuery(id))));

        group.MapPost("/{id:guid}/send", async (Guid id, ISender sender) =>
            Results.Accepted(value: await sender.Send(new SendCampaignCommand(id))));

        group.MapPost("/{id:guid}/send-test", async (Guid id, SendTestBody body, ISender sender) =>
        {
            await sender.Send(new SendTestCommand(id, body.Recipients));
            return Results.Accepted();
        });

        return app;
    }
}

public sealed record UpdateCampaignBody(string Name, DateTimeOffset? ScheduledAt, string? Timezone, List<string>? Tags);
public sealed record ChangeStatusBody(CampaignStatus Status);
public sealed record SendTestBody(IReadOnlyList<string> Recipients);
public sealed record ContentBody(
    string? SubjectLine, string? PreviewText, string? FromName, string? FromEmail, string? ReplyTo,
    string? HtmlBody, string? TextBody, string? SenderId, string? SmsBody, bool TrackLinks);
