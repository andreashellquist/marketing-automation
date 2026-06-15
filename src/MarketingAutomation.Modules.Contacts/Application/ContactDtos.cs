using MarketingAutomation.Modules.Contacts.Domain;
using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Contacts.Application;

public sealed record IdentifierDto(IdentifierType Type, string Value);

public sealed record ConsentStateDto(Channel Channel, ConsentPurpose Purpose, ConsentStatus Status, DateTimeOffset RecordedAt);

public sealed record ContactDto(
    Guid Id,
    string? Email,
    string? Phone,
    string? FirstName,
    string? LastName,
    string? Locale,
    string? Timezone,
    Dictionary<string, object?> CustomAttributes,
    IReadOnlyList<IdentifierDto> Identifiers,
    IReadOnlyList<ConsentStateDto> Consent,
    DateTimeOffset CreatedAt)
{
    public static ContactDto From(Contact c)
    {
        var currentConsent = c.ConsentEntries
            .GroupBy(e => new { e.Channel, e.Purpose })
            .Select(g => g.OrderByDescending(e => e.RecordedAt).First())
            .Select(e => new ConsentStateDto(e.Channel, e.Purpose, e.Status, e.RecordedAt))
            .ToList();

        return new ContactDto(
            c.Id, c.Email, c.Phone, c.FirstName, c.LastName, c.Locale, c.Timezone,
            c.CustomAttributes,
            c.Identifiers.Select(i => new IdentifierDto(i.Type, i.Value)).ToList(),
            currentConsent,
            c.CreatedAt);
    }
}

public sealed record ContactSummaryDto(Guid Id, string? Email, string? Phone, string? FirstName, string? LastName, DateTimeOffset CreatedAt);
