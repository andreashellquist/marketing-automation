using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Messaging.Domain;

public sealed record OutboundMessage(
    Channel Channel, string Recipient, string? Subject, string Body, string? FromName, string? FromAddress);

public sealed record SendOutcome(bool Success, string Provider, string? ProviderMessageId, string? Error)
{
    public static SendOutcome Ok(string provider, string providerMessageId) =>
        new(true, provider, providerMessageId, null);
    public static SendOutcome Fail(string provider, string error) =>
        new(false, provider, null, error);
}

/// <summary>
/// A delivery provider for a single channel. Real providers (SendGrid, Twilio, FCM/APNs,
/// or SMTP→Mailpit in dev) implement this; the pipeline resolves one by <see cref="Channel"/>.
/// </summary>
public interface IChannelSender
{
    Channel Channel { get; }
    Task<SendOutcome> SendAsync(OutboundMessage message, CancellationToken ct);
}
