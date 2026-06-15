using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Contacts.Infrastructure;

/// <summary>Send-time suppression lookup for the Messaging pipeline (SharedKernel contract).</summary>
public sealed class SuppressionChecker(ContactsDbContext db) : ISuppressionChecker
{
    public Task<bool> IsSuppressedAsync(Channel channel, string normalizedValue, CancellationToken ct) =>
        db.SuppressionEntries.AnyAsync(s => s.Channel == channel && s.Value == normalizedValue, ct);
}
