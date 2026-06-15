using MarketingAutomation.Modules.Contacts.Infrastructure;
using MarketingAutomation.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Contacts.Application;

public sealed record ListContactsQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ContactSummaryDto>>;

public sealed class ListContactsHandler(ContactsDbContext db)
    : IRequestHandler<ListContactsQuery, PagedResult<ContactSummaryDto>>
{
    public async Task<PagedResult<ContactSummaryDto>> Handle(ListContactsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = db.Contacts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                (c.FirstName != null && c.FirstName.ToLower().Contains(term)) ||
                (c.LastName != null && c.LastName.ToLower().Contains(term)));
        }

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ContactSummaryDto(c.Id, c.Email, c.Phone, c.FirstName, c.LastName, c.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<ContactSummaryDto>(items, page, pageSize, total);
    }
}
