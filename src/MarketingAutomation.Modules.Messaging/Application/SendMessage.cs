using FluentValidation;
using MarketingAutomation.Modules.Messaging.Domain;
using MarketingAutomation.Modules.Messaging.Infrastructure;
using MarketingAutomation.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Messaging.Application;

/// <summary>
/// Sends one message through the full pipeline: idempotency check → policy gate →
/// provider send → status record. The same <see cref="DedupKey"/> never sends twice.
/// </summary>
public sealed record SendMessageCommand(
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
    Guid? SourceId = null)
    : IRequest<SendMessageResult>;

public sealed record SendMessageResult(Guid MessageId, MessageStatus Status, string? Reason);

public sealed class SendMessageValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageValidator()
    {
        RuleFor(c => c.Recipient).NotEmpty();
        RuleFor(c => c.Body).NotEmpty();
        RuleFor(c => c.DedupKey).NotEmpty().MaximumLength(320);
    }
}

public sealed class SendMessageHandler(
    MessagingDbContext db,
    ITenantContext tenantContext,
    SendPolicyGate gate,
    IEnumerable<IChannelSender> senders)
    : IRequestHandler<SendMessageCommand, SendMessageResult>
{
    public async Task<SendMessageResult> Handle(SendMessageCommand request, CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId;

        // Idempotency: a dedup key that already produced a message returns that message.
        var existing = await db.Messages.FirstOrDefaultAsync(m => m.DedupKey == request.DedupKey, ct);
        if (existing is not null)
            return new SendMessageResult(existing.Id, existing.Status, existing.StatusReason);

        var message = new Message
        {
            Channel = request.Channel,
            Purpose = request.Purpose,
            ContactId = request.ContactId,
            Recipient = request.Recipient,
            RecipientTimezone = request.RecipientTimezone,
            Subject = request.Subject,
            Body = request.Body,
            FromName = request.FromName,
            FromAddress = request.FromAddress,
            DedupKey = request.DedupKey,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
        };

        var decision = await gate.EvaluateAsync(
            tenantId, request.Channel, request.Purpose, request.Recipient,
            request.RecipientTimezone, DateTimeOffset.UtcNow, ct);

        switch (decision.Outcome)
        {
            case PolicyOutcome.Suppress:
                message.MarkSuppressed(decision.Reason!);
                break;
            case PolicyOutcome.Hold:
                message.MarkHeld(decision.Reason!);
                break;
            case PolicyOutcome.Allow:
                await DispatchAsync(message, tenantId, ct);
                break;
        }

        db.Messages.Add(message);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race on the unique dedup key; return the winner if that's the cause.
            var dup = await db.Messages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.DedupKey == request.DedupKey && m.Id != message.Id, ct);
            if (dup is null) throw;
            return new SendMessageResult(dup.Id, dup.Status, dup.StatusReason);
        }

        return new SendMessageResult(message.Id, message.Status, message.StatusReason);
    }

    private async Task DispatchAsync(Message message, Guid tenantId, CancellationToken ct)
    {
        var sender = senders.FirstOrDefault(s => s.Channel == message.Channel)
            ?? throw new InvalidOperationException($"No sender registered for channel {message.Channel}.");

        var outcome = await sender.SendAsync(new OutboundMessage(
            message.Channel, message.Recipient, message.Subject, message.Body,
            message.FromName, message.FromAddress), ct);

        if (outcome.Success)
        {
            message.MarkSent(outcome.Provider, outcome.ProviderMessageId);
            message.RaiseIntegrationEvent(new MessageSent(
                Guid.CreateVersion7(), tenantId, DateTimeOffset.UtcNow,
                message.Id, message.Channel, message.ContactId));
        }
        else
        {
            message.MarkFailed(outcome.Provider, outcome.Error ?? "send_failed");
        }
    }
}
