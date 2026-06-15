using MarketingAutomation.Modules.Contacts.Application;
using MarketingAutomation.Modules.Contacts.Domain;
using MarketingAutomation.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MarketingAutomation.Modules.Contacts.Endpoints;

public static class ContactsEndpoints
{
    public static IEndpointRouteBuilder MapContactsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/contacts").WithTags("Contacts");

        group.MapPost("/identify", async (IdentifyContactRequest body, ISender sender) =>
        {
            var dto = await sender.Send(new IdentifyContactCommand(
                body.Identifiers,
                new ContactTraits(body.Email, body.Phone, body.FirstName, body.LastName,
                    body.Locale, body.Timezone, body.Attributes)));
            return Results.Ok(dto);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
            Results.Ok(await sender.Send(new GetContactQuery(id))));

        group.MapGet("/", async (string? search, int? page, int? pageSize, ISender sender) =>
            Results.Ok(await sender.Send(new ListContactsQuery(search, page ?? 1, pageSize ?? 20))));

        group.MapPost("/{id:guid}/consent", async (Guid id, ConsentRequest body, ISender sender) =>
            Results.Ok(await sender.Send(new UpdateConsentCommand(
                id, body.Channel, body.Purpose, body.Status, body.Source, body.IpAddress, body.ConsentText))));

        group.MapPost("/suppressions", async (AddSuppressionCommand body, ISender sender) =>
        {
            var id = await sender.Send(body);
            return Results.Created($"/api/v1/contacts/suppressions/{id}", new { id });
        });

        return app;
    }
}

public sealed record IdentifyContactRequest(
    IReadOnlyList<IdentifierDto> Identifiers,
    string? Email = null,
    string? Phone = null,
    string? FirstName = null,
    string? LastName = null,
    string? Locale = null,
    string? Timezone = null,
    Dictionary<string, object?>? Attributes = null);

public sealed record ConsentRequest(
    Channel Channel,
    ConsentPurpose Purpose,
    ConsentStatus Status,
    string Source,
    string? IpAddress = null,
    string? ConsentText = null);
