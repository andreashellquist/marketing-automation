using MarketingAutomation.Modules.Events.Application;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MarketingAutomation.Modules.Events.Endpoints;

public static class EventsEndpoints
{
    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/events").WithTags("Events");

        // Single event — convenience wrapper over the batch path.
        group.MapPost("/", async (EventInput body, ISender sender) =>
        {
            var result = await sender.Send(new IngestEventsCommand([body]));
            return Results.Accepted(value: result);
        });

        group.MapPost("/batch", async (BatchEventsRequest body, ISender sender) =>
        {
            var result = await sender.Send(new IngestEventsCommand(body.Events));
            return Results.Accepted(value: result);
        });

        return app;
    }
}

public sealed record BatchEventsRequest(IReadOnlyList<EventInput> Events);
