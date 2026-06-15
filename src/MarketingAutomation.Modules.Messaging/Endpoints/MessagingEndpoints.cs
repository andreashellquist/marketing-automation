using MarketingAutomation.Modules.Messaging.Application;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MarketingAutomation.Modules.Messaging.Endpoints;

public static class MessagingEndpoints
{
    public static IEndpointRouteBuilder MapMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/messages").WithTags("Messaging");

        // Direct send — primarily for testing; campaigns/journeys call the handler internally.
        group.MapPost("/send", async (SendMessageCommand body, ISender sender) =>
        {
            var result = await sender.Send(body);
            return Results.Accepted(value: result);
        });

        // Provider delivery receipts (DLR / bounce webhooks).
        group.MapPost("/delivery-status", async (UpdateDeliveryStatusCommand body, ISender sender) =>
        {
            var result = await sender.Send(body);
            return Results.Ok(result);
        });

        return app;
    }
}
