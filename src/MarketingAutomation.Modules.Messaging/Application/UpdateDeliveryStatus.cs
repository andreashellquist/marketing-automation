using MarketingAutomation.Modules.Messaging.Domain;
using MarketingAutomation.Modules.Messaging.Infrastructure;
using MarketingAutomation.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Messaging.Application;

public enum DeliveryStatus { Delivered = 1, Bounced = 2, Failed = 3 }

/// <summary>
/// Applies a provider delivery receipt (DLR/bounce webhook). Idempotent: receipts for
/// unknown or already-terminal messages are ignored, so duplicate webhook deliveries
/// never double-process.
/// </summary>
public sealed record UpdateDeliveryStatusCommand(
    string Provider,
    string ProviderMessageId,
    DeliveryStatus Status,
    bool IsHardBounce = false,
    string? Reason = null)
    : IRequest<DeliveryUpdateResult>;

public sealed record DeliveryUpdateResult(bool Applied, MessageStatus? Status);

public sealed class UpdateDeliveryStatusHandler(MessagingDbContext db, ITenantContext tenantContext)
    : IRequestHandler<UpdateDeliveryStatusCommand, DeliveryUpdateResult>
{
    private static readonly MessageStatus[] Terminal =
        [MessageStatus.Delivered, MessageStatus.Bounced];

    public async Task<DeliveryUpdateResult> Handle(UpdateDeliveryStatusCommand request, CancellationToken ct)
    {
        var message = await db.Messages.FirstOrDefaultAsync(
            m => m.Provider == request.Provider && m.ProviderMessageId == request.ProviderMessageId, ct);

        // Unknown receipt or already finalized — ignore.
        if (message is null || Terminal.Contains(message.Status))
            return new DeliveryUpdateResult(false, message?.Status);

        switch (request.Status)
        {
            case DeliveryStatus.Delivered:
                message.Status = MessageStatus.Delivered;
                message.DeliveredAt = DateTimeOffset.UtcNow;
                message.RaiseIntegrationEvent(new MessageDelivered(
                    Guid.CreateVersion7(), tenantContext.TenantId, DateTimeOffset.UtcNow, message.Id));
                break;

            case DeliveryStatus.Bounced:
                message.Status = MessageStatus.Bounced;
                message.StatusReason = request.Reason;
                message.RaiseIntegrationEvent(new MessageBounced(
                    Guid.CreateVersion7(), tenantContext.TenantId, DateTimeOffset.UtcNow,
                    message.Id, message.Channel, message.Recipient, request.IsHardBounce));
                break;

            case DeliveryStatus.Failed:
                message.Status = MessageStatus.Failed;
                message.StatusReason = request.Reason;
                break;
        }

        await db.SaveChangesAsync(ct);
        return new DeliveryUpdateResult(true, message.Status);
    }
}
