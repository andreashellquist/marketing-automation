using MarketingAutomation.Modules.Templates.Application;
using MarketingAutomation.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MarketingAutomation.Modules.Templates.Endpoints;

public static class TemplatesEndpoints
{
    public static IEndpointRouteBuilder MapTemplatesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/templates").WithTags("Templates");

        group.MapPost("/", async (CreateTemplateBody body, ISender sender) =>
        {
            var dto = await sender.Send(new CreateTemplateCommand(
                body.Name, body.Channel ?? Channel.Email, body.Subject, body.HtmlBody, body.TextBody, body.DesignJson));
            return Results.Created($"/api/v1/templates/{dto.Id}", dto);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
            Results.Ok(await sender.Send(new GetTemplateQuery(id))));

        group.MapPut("/{id:guid}", async (Guid id, UpdateTemplateBody body, ISender sender) =>
            Results.Ok(await sender.Send(new UpdateTemplateCommand(
                id, body.Name, body.Subject, body.HtmlBody, body.TextBody, body.DesignJson))));

        group.MapPost("/{id:guid}/preview", async (Guid id, PreviewBody body, ISender sender) =>
            Results.Ok(await sender.Send(new PreviewTemplateCommand(id, body.Data ?? new()))));

        group.MapGet("/{id:guid}/preflight", async (Guid id, ISender sender) =>
            Results.Ok(await sender.Send(new PreflightTemplateQuery(id))));

        app.MapGet("/api/v1/brand-kit", () => Results.NoContent()); // read endpoint placeholder
        app.MapPut("/api/v1/brand-kit", async (BrandKitBody body, ISender sender) =>
        {
            await sender.Send(new SetBrandKitCommand(
                body.LogoUrl, body.PrimaryColor, body.FontFamily, body.FooterHtml, body.CompanyAddress));
            return Results.NoContent();
        }).WithTags("Templates");

        return app;
    }
}

public sealed record CreateTemplateBody(
    string Name, Channel? Channel, string? Subject, string HtmlBody, string? TextBody, string? DesignJson);
public sealed record UpdateTemplateBody(string Name, string? Subject, string HtmlBody, string? TextBody, string? DesignJson);
public sealed record PreviewBody(Dictionary<string, object?>? Data);
public sealed record BrandKitBody(string? LogoUrl, string? PrimaryColor, string? FontFamily, string? FooterHtml, string? CompanyAddress);
