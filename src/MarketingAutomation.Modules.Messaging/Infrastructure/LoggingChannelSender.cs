using MarketingAutomation.Modules.Messaging.Domain;
using MarketingAutomation.SharedKernel;
using Microsoft.Extensions.Logging;

namespace MarketingAutomation.Modules.Messaging.Infrastructure;

/// <summary>
/// Default dev sender: logs the message and returns a synthetic provider id. Real
/// providers (SendGrid/Twilio/FCM/SMTP) replace these per channel without touching
/// the pipeline.
/// </summary>
public sealed class LoggingChannelSender(Channel channel, ILogger<LoggingChannelSender> logger) : IChannelSender
{
    public Channel Channel => channel;

    public Task<SendOutcome> SendAsync(OutboundMessage message, CancellationToken ct)
    {
        var providerMessageId = $"log-{Guid.CreateVersion7():N}";
        logger.LogInformation(
            "[{Channel}] -> {Recipient} subject={Subject} (providerMessageId={ProviderMessageId})",
            message.Channel, message.Recipient, message.Subject ?? "(none)", providerMessageId);

        return Task.FromResult(SendOutcome.Ok("log", providerMessageId));
    }
}
