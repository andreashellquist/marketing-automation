using MarketingAutomation.Modules.Messaging.Domain;
using MarketingAutomation.Modules.Messaging.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Messaging.Application;

public enum PolicyOutcome { Allow, Suppress, Hold }

public sealed record PolicyDecision(PolicyOutcome Outcome, string? Reason)
{
    public static readonly PolicyDecision Allowed = new(PolicyOutcome.Allow, null);
    public static PolicyDecision Suppress(string reason) => new(PolicyOutcome.Suppress, reason);
    public static PolicyDecision Hold(string reason) => new(PolicyOutcome.Hold, reason);
}

/// <summary>
/// The single send-time gate every message passes through, regardless of channel or
/// source. Evaluated as late as possible so opt-outs and kill switches that happen
/// after scheduling are still honored. Transactional messages bypass marketing policy.
/// </summary>
public sealed class SendPolicyGate(
    ISendingControl sendingControl,
    ISuppressionChecker suppressionChecker,
    MessagingDbContext db)
{
    public async Task<PolicyDecision> EvaluateAsync(
        Guid tenantId,
        Channel channel,
        MessagePurpose purpose,
        string recipient,
        string? recipientTimezone,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var policy = await sendingControl.GetPolicyAsync(tenantId, ct);

        // Kill switch holds (not drops) so messages can resume when re-enabled.
        if (policy.SendingPaused)
            return PolicyDecision.Hold("sending_paused");

        // Transactional traffic is exempt from marketing policy.
        if (purpose == MessagePurpose.Transactional)
            return PolicyDecision.Allowed;

        if (await suppressionChecker.IsSuppressedAsync(channel, recipient, ct))
            return PolicyDecision.Suppress("suppressed");

        if (policy.QuietHoursEnabled &&
            QuietHours.IsWithin(now, recipientTimezone, policy.QuietStartHour, policy.QuietEndHour))
            return PolicyDecision.Hold("quiet_hours");

        if (policy.MaxMarketingPerDay is { } cap)
        {
            // Compare on UTC ticks: SQLite (used in tests) can't range-query DateTimeOffset,
            // and ticks order identically to the instant on every provider.
            var sinceTicks = (now - TimeSpan.FromDays(1)).UtcTicks;
            var recentCount = await db.Messages
                .Where(m => m.Recipient == recipient)
                .Where(m => m.Purpose == MessagePurpose.Marketing)
                .Where(m => m.Status == MessageStatus.Sent || m.Status == MessageStatus.Delivered)
                .Where(m => m.SentAtTicks != null && m.SentAtTicks >= sinceTicks)
                .CountAsync(ct);

            if (recentCount >= cap)
                return PolicyDecision.Suppress("frequency_capped");
        }

        return PolicyDecision.Allowed;
    }
}
