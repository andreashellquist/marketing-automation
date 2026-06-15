using MarketingAutomation.Modules.Contacts.Domain;
using MarketingAutomation.Modules.Contacts.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Contacts.Application;

/// <summary>
/// Appends a consent entry. Revoking marketing consent also writes a suppression entry
/// so the send-time gate honors it across channels, even mid-campaign.
/// </summary>
public sealed record UpdateConsentCommand(
    Guid ContactId,
    Channel Channel,
    ConsentPurpose Purpose,
    ConsentStatus Status,
    string Source,
    string? IpAddress = null,
    string? ConsentText = null)
    : IRequest<ContactDto>;

public sealed class UpdateConsentHandler(ContactsDbContext db)
    : IRequestHandler<UpdateConsentCommand, ContactDto>
{
    public async Task<ContactDto> Handle(UpdateConsentCommand request, CancellationToken ct)
    {
        var contact = await db.Contacts
            .Include(c => c.Identifiers)
            .Include(c => c.ConsentEntries)
            .FirstOrDefaultAsync(c => c.Id == request.ContactId, ct)
            ?? throw new NotFoundException("Contact", request.ContactId);

        contact.RecordConsent(request.Channel, request.Purpose, request.Status,
            request.Source, request.IpAddress, request.ConsentText);

        contact.RaiseIntegrationEvent(new ConsentChanged(
            Guid.CreateVersion7(), contact.TenantId, DateTimeOffset.UtcNow,
            contact.Id, request.Channel, request.Purpose, request.Status));

        if (request is { Purpose: ConsentPurpose.Marketing, Status: ConsentStatus.Revoked })
        {
            await SuppressChannelAddress(contact, request.Channel, ct);
        }

        await db.SaveChangesAsync(ct);
        return ContactDto.From(contact);
    }

    private async Task SuppressChannelAddress(Contact contact, Channel channel, CancellationToken ct)
    {
        var value = channel switch
        {
            Channel.Email when contact.Email is not null => Normalize.Email(contact.Email),
            Channel.Sms or Channel.WhatsApp when contact.Phone is not null => Normalize.Phone(contact.Phone),
            _ => null,
        };
        if (value is null) return;

        var exists = await db.SuppressionEntries.AnyAsync(s => s.Channel == channel && s.Value == value, ct);
        if (exists) return;

        db.SuppressionEntries.Add(new SuppressionEntry
        {
            Channel = channel,
            Value = value,
            Reason = SuppressionReason.Unsubscribe,
        });
    }
}
