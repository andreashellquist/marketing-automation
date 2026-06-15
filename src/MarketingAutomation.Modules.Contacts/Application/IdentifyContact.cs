using FluentValidation;
using MarketingAutomation.Modules.Contacts.Domain;
using MarketingAutomation.Modules.Contacts.Infrastructure;
using MarketingAutomation.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Contacts.Application;

/// <summary>
/// Upserts a contact from a set of identifiers plus traits — the entry point for both
/// server-side identify calls and anonymous-to-known merges. Resolution is deterministic:
/// any matching identifier resolves to an existing contact; multiple matches merge into
/// the oldest (its Guid v7 sorts first).
/// </summary>
public sealed record IdentifyContactCommand(
    IReadOnlyList<IdentifierDto> Identifiers,
    ContactTraits Traits)
    : IRequest<ContactDto>;

public sealed class IdentifyContactValidator : AbstractValidator<IdentifyContactCommand>
{
    public IdentifyContactValidator()
    {
        RuleFor(c => c.Identifiers).NotEmpty().WithMessage("At least one identifier is required.");
        RuleForEach(c => c.Identifiers).ChildRules(i =>
            i.RuleFor(x => x.Value).NotEmpty());
    }
}

public sealed class IdentifyContactHandler(ContactsDbContext db, ITenantContext tenantContext)
    : IRequestHandler<IdentifyContactCommand, ContactDto>
{
    public async Task<ContactDto> Handle(IdentifyContactCommand request, CancellationToken ct)
    {
        var normalized = request.Identifiers
            .Select(i => new IdentifierDto(i.Type, Normalize.Identifier(i.Type, i.Value)))
            .DistinctBy(i => (i.Type, i.Value))
            .ToList();

        var lookupValues = normalized.Select(i => i.Value).ToList();

        var matches = await db.Contacts
            .Include(c => c.Identifiers)
            .Include(c => c.ConsentEntries)
            .Where(c => c.Identifiers.Any(i => lookupValues.Contains(i.Value)))
            .ToListAsync(ct);

        Contact contact;
        var isNew = false;

        if (matches.Count == 0)
        {
            contact = new Contact();
            db.Contacts.Add(contact);
            isNew = true;
        }
        else
        {
            // Oldest contact (smallest Guid v7) is the survivor; fold the rest in.
            contact = matches.OrderBy(c => c.Id).First();
            foreach (var duplicate in matches.Where(c => c.Id != contact.Id))
            {
                MergeInto(contact, duplicate);
                db.Contacts.Remove(duplicate); // soft delete via base SaveChanges
            }
            db.ChangeTracker.DetectChanges();
        }

        contact.ApplyTraits(request.Traits);
        foreach (var identifier in normalized)
        {
            contact.AddIdentifierIfMissing(identifier.Type, identifier.Value);
        }

        contact.RaiseIntegrationEvent(new ContactIdentified(
            Guid.CreateVersion7(), tenantContext.TenantId, DateTimeOffset.UtcNow, contact.Id, isNew));

        await db.SaveChangesAsync(ct);
        return ContactDto.From(contact);
    }

    private static void MergeInto(Contact survivor, Contact duplicate)
    {
        // Survivor's own non-null traits win; the duplicate only fills gaps.
        survivor.ApplyTraits(new ContactTraits(
            survivor.Email ?? duplicate.Email,
            survivor.Phone ?? duplicate.Phone,
            survivor.FirstName ?? duplicate.FirstName,
            survivor.LastName ?? duplicate.LastName,
            survivor.Locale ?? duplicate.Locale,
            survivor.Timezone ?? duplicate.Timezone,
            duplicate.CustomAttributes));

        // Move identifier rows (don't copy) so the unique index never sees two active rows.
        foreach (var identifier in duplicate.Identifiers.ToList())
        {
            var clash = survivor.Identifiers.Any(i => i.Type == identifier.Type && i.Value == identifier.Value);
            if (clash)
            {
                identifier.IsDeleted = true; // excluded by the filtered unique index
            }
            else
            {
                identifier.ContactId = survivor.Id;
                duplicate.Identifiers.Remove(identifier);
                survivor.Identifiers.Add(identifier);
            }
        }

        // Preserve the duplicate's consent ledger under the survivor.
        foreach (var consent in duplicate.ConsentEntries.ToList())
        {
            consent.ContactId = survivor.Id;
            duplicate.ConsentEntries.Remove(consent);
            survivor.ConsentEntries.Add(consent);
        }
    }
}
