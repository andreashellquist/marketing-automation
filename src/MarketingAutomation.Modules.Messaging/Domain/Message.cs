using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Messaging.Domain;

public enum MessagePurpose
{
    Marketing = 1,
    Transactional = 2,
}

public enum MessageStatus
{
    /// <summary>Created, not yet handed to a provider.</summary>
    Queued = 1,
    /// <summary>Accepted by the provider.</summary>
    Sent = 2,
    /// <summary>Provider confirmed delivery (DLR).</summary>
    Delivered = 3,
    /// <summary>Provider reported a bounce.</summary>
    Bounced = 4,
    /// <summary>Provider/transport error.</summary>
    Failed = 5,
    /// <summary>Blocked by policy (suppression / consent / frequency cap).</summary>
    Suppressed = 6,
    /// <summary>Deferred by the kill switch or quiet hours.</summary>
    Held = 7,
}

/// <summary>
/// One outbound message to one recipient on one channel. The <see cref="DedupKey"/>
/// guarantees at-most-once sending for a given (campaign|journey step, contact).
/// </summary>
public sealed class Message : TenantEntity
{
    public Channel Channel { get; set; }
    public MessagePurpose Purpose { get; set; }
    public Guid? ContactId { get; set; }

    /// <summary>Normalized recipient address (lowercased email / E.164 phone / device token).</summary>
    public required string Recipient { get; set; }
    public string? RecipientTimezone { get; set; }

    public string? Subject { get; set; }
    public required string Body { get; set; }
    public string? FromName { get; set; }
    public string? FromAddress { get; set; }

    public MessageStatus Status { get; set; } = MessageStatus.Queued;
    public string? StatusReason { get; set; }

    public string? Provider { get; set; }
    public string? ProviderMessageId { get; set; }

    /// <summary>At-most-once key, unique per tenant.</summary>
    public required string DedupKey { get; set; }

    /// <summary>Originating campaign / journey step (free-form), for attribution.</summary>
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    /// <summary>UTC ticks mirror of <see cref="SentAt"/> for provider-portable range queries
    /// (the frequency-cap window). Set together with <see cref="SentAt"/>.</summary>
    public long? SentAtTicks { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public void MarkSuppressed(string reason)
    {
        Status = MessageStatus.Suppressed;
        StatusReason = reason;
    }

    public void MarkHeld(string reason)
    {
        Status = MessageStatus.Held;
        StatusReason = reason;
    }

    public void MarkSent(string provider, string? providerMessageId)
    {
        Status = MessageStatus.Sent;
        Provider = provider;
        ProviderMessageId = providerMessageId;
        SentAt = DateTimeOffset.UtcNow;
        SentAtTicks = SentAt.Value.UtcTicks;
    }

    public void MarkFailed(string provider, string error)
    {
        Status = MessageStatus.Failed;
        Provider = provider;
        StatusReason = error;
    }
}
