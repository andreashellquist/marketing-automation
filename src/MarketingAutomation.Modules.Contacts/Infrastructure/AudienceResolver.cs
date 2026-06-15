using System.Runtime.CompilerServices;
using MarketingAutomation.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Contacts.Infrastructure;

/// <summary>
/// Resolves a campaign/journey audience from the contact base (SharedKernel contract).
/// Segment-based selection is delegated to the Segments module once it lands; for now a
/// null selector streams the whole tenant base and a non-null one is treated the same
/// (no segments yet) — the contract is stable so callers don't change later.
/// </summary>
public sealed class AudienceResolver(ContactsDbContext db) : IAudienceResolver
{
    public async IAsyncEnumerable<AudienceMember> ResolveAsync(
        Guid? segmentId, [EnumeratorCancellation] CancellationToken ct)
    {
        var query = db.Contacts
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new AudienceMember(c.Id, c.Email, c.Phone, c.Timezone))
            .AsAsyncEnumerable();

        await foreach (var member in query.WithCancellation(ct))
        {
            yield return member;
        }
    }
}
