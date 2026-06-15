using FluentValidation;
using MarketingAutomation.Modules.Contacts.Domain;
using MarketingAutomation.Modules.Contacts.Infrastructure;
using MarketingAutomation.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Contacts.Application;

public sealed record AddSuppressionCommand(
    Channel Channel, string Value, SuppressionReason Reason, string? Notes = null)
    : IRequest<Guid>;

public sealed class AddSuppressionValidator : AbstractValidator<AddSuppressionCommand>
{
    public AddSuppressionValidator() => RuleFor(c => c.Value).NotEmpty();
}

public sealed class AddSuppressionHandler(ContactsDbContext db, ITenantContext tenantContext)
    : IRequestHandler<AddSuppressionCommand, Guid>
{
    public async Task<Guid> Handle(AddSuppressionCommand request, CancellationToken ct)
    {
        var value = Normalize.Identifier(
            request.Channel == Channel.Email ? IdentifierType.Email : IdentifierType.Phone, request.Value);

        var existing = await db.SuppressionEntries
            .FirstOrDefaultAsync(s => s.Channel == request.Channel && s.Value == value, ct);
        if (existing is not null) return existing.Id;

        var entry = new SuppressionEntry
        {
            Channel = request.Channel,
            Value = value,
            Reason = request.Reason,
            Notes = request.Notes,
        };
        entry.RaiseIntegrationEvent(new ContactSuppressed(
            Guid.CreateVersion7(), tenantContext.TenantId, DateTimeOffset.UtcNow,
            request.Channel, value, request.Reason));

        db.SuppressionEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry.Id;
    }
}
