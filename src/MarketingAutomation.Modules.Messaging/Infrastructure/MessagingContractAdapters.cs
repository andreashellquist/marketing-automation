using MarketingAutomation.Modules.Messaging.Application;
using MarketingAutomation.Modules.Messaging.Domain;
using MarketingAutomation.SharedKernel.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Messaging.Infrastructure;

/// <summary>Exposes the send pipeline to other modules via the SharedKernel contract.</summary>
public sealed class MessageSenderAdapter(ISender sender) : IMessageSender
{
    public Task SendAsync(SendRequest request, CancellationToken ct) =>
        sender.Send(new SendMessageCommand(
            request.Channel, request.Purpose, request.Recipient, request.Body, request.DedupKey,
            request.Subject, request.RecipientTimezone, request.ContactId,
            request.FromName, request.FromAddress, request.SourceType, request.SourceId), ct);
}

/// <summary>Aggregates message outcomes for a source (campaign/journey) into delivery stats.</summary>
public sealed class MessageStatsProvider(MessagingDbContext db) : IMessageStatsProvider
{
    public async Task<MessageStats> GetForSourceAsync(string sourceType, Guid sourceId, CancellationToken ct)
    {
        var counts = await db.Messages
            .Where(m => m.SourceType == sourceType && m.SourceId == sourceId)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(MessageStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;

        return new MessageStats(
            Total: counts.Sum(c => c.Count),
            Sent: CountOf(MessageStatus.Sent),
            Delivered: CountOf(MessageStatus.Delivered),
            Bounced: CountOf(MessageStatus.Bounced),
            Failed: CountOf(MessageStatus.Failed),
            Suppressed: CountOf(MessageStatus.Suppressed),
            Held: CountOf(MessageStatus.Held));
    }
}
