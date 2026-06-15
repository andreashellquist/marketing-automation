using MarketingAutomation.Modules.Contacts.Infrastructure;
using MarketingAutomation.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Contacts.Application;

public sealed record GetContactQuery(Guid Id) : IRequest<ContactDto>;

public sealed class GetContactHandler(ContactsDbContext db) : IRequestHandler<GetContactQuery, ContactDto>
{
    public async Task<ContactDto> Handle(GetContactQuery request, CancellationToken ct)
    {
        var contact = await db.Contacts
            .Include(c => c.Identifiers)
            .Include(c => c.ConsentEntries)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct)
            ?? throw new NotFoundException("Contact", request.Id);

        return ContactDto.From(contact);
    }
}
