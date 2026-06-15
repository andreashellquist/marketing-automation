namespace MarketingAutomation.SharedKernel.Contracts;

/// <summary>
/// Contracts that let Campaigns (and later Journeys) drive sends without referencing
/// Messaging or Contacts. The owning modules implement them; callers depend only on
/// SharedKernel.
/// </summary>

/// <summary>One resolved recipient of an audience (implemented by Contacts/Segments).</summary>
public sealed record AudienceMember(Guid ContactId, string? Email, string? Phone, string? Timezone);

public interface IAudienceResolver
{
    /// <summary>Streams audience members. A null selector means the entire contact base.</summary>
    IAsyncEnumerable<AudienceMember> ResolveAsync(Guid? segmentId, CancellationToken ct);
}

/// <summary>Neutral send request routed to the Messaging pipeline (implemented by Messaging).</summary>
public sealed record SendRequest(
    Channel Channel,
    MessagePurpose Purpose,
    string Recipient,
    string Body,
    string DedupKey,
    string? Subject = null,
    string? RecipientTimezone = null,
    Guid? ContactId = null,
    string? FromName = null,
    string? FromAddress = null,
    string? SourceType = null,
    Guid? SourceId = null);

public interface IMessageSender
{
    Task SendAsync(SendRequest request, CancellationToken ct);
}

/// <summary>Delivery counts for a source (campaign/journey), implemented by Messaging.</summary>
public sealed record MessageStats(
    int Total, int Sent, int Delivered, int Bounced, int Failed, int Suppressed, int Held);

public interface IMessageStatsProvider
{
    Task<MessageStats> GetForSourceAsync(string sourceType, Guid sourceId, CancellationToken ct);
}
