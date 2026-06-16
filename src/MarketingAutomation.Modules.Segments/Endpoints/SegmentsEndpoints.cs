using MarketingAutomation.Modules.Segments.Application;
using MarketingAutomation.Modules.Segments.Domain;
using MarketingAutomation.SharedKernel.Segments;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MarketingAutomation.Modules.Segments.Endpoints;

public static class SegmentsEndpoints
{
    public static IEndpointRouteBuilder MapSegmentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/segments").WithTags("Segments");

        group.MapGet("/", async (string? search, int? page, int? pageSize, ISender sender) =>
            Results.Ok(await sender.Send(new ListSegmentsQuery(search, page ?? 1, pageSize ?? 20))));

        group.MapPost("/", async (CreateSegmentBody body, ISender sender) =>
        {
            var dto = await sender.Send(new CreateSegmentCommand(
                body.Name, body.Type ?? SegmentType.Dynamic, body.Definition ?? new SegmentGroup()));
            return Results.Created($"/api/v1/segments/{dto.Id}", dto);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
            Results.Ok(await sender.Send(new GetSegmentQuery(id))));

        group.MapPut("/{id:guid}", async (Guid id, UpdateSegmentBody body, ISender sender) =>
            Results.Ok(await sender.Send(new UpdateSegmentCommand(id, body.Name, body.Definition))));

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            await sender.Send(new DeleteSegmentCommand(id));
            return Results.NoContent();
        });

        // Live count + sample for the builder, without persisting.
        group.MapPost("/preview", async (PreviewBody body, ISender sender) =>
            Results.Ok(await sender.Send(new PreviewSegmentCommand(body.Definition, body.SampleSize ?? 10))));

        // AI: natural language -> AST (returned for confirmation in the visual builder).
        group.MapPost("/from-text", async (FromTextBody body, ISender sender) =>
            Results.Ok(await sender.Send(new BuildSegmentFromTextCommand(body.Description))));

        return app;
    }
}

public sealed record CreateSegmentBody(string Name, SegmentType? Type, SegmentGroup? Definition);
public sealed record UpdateSegmentBody(string Name, SegmentGroup Definition);
public sealed record PreviewBody(SegmentGroup Definition, int? SampleSize);
public sealed record FromTextBody(string Description);
