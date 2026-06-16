using MarketingAutomation.Modules.Journeys.Application;
using MarketingAutomation.Modules.Journeys.Domain;
using MarketingAutomation.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MarketingAutomation.Modules.Journeys.Endpoints;

public static class JourneysEndpoints
{
    public static IEndpointRouteBuilder MapJourneysEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/journeys").WithTags("Journeys");

        group.MapPost("/", async (CreateJourneyBody body, ISender sender) =>
        {
            var dto = await sender.Send(new CreateJourneyCommand(
                body.Name, body.Channel ?? Channel.Email, body.ReentryPolicy ?? ReentryPolicy.Never,
                body.StartNodeId, body.Nodes ?? []));
            return Results.Created($"/api/v1/journeys/{dto.Id}", dto);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
            Results.Ok(await sender.Send(new GetJourneyQuery(id))));

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender) =>
            Results.Ok(await sender.Send(new ActivateJourneyCommand(id))));

        group.MapPost("/{id:guid}/enroll", async (Guid id, EnrollBody body, ISender sender) =>
            Results.Accepted(value: await sender.Send(new EnrollContactCommand(
                id, body.ContactId, body.Recipient, body.RecipientTimezone))));

        return app;
    }
}

public sealed record CreateJourneyBody(
    string Name, Channel? Channel, ReentryPolicy? ReentryPolicy, string? StartNodeId, List<JourneyNode>? Nodes);

public sealed record EnrollBody(Guid ContactId, string Recipient, string? RecipientTimezone);
