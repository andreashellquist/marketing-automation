using FluentValidation;
using MarketingAutomation.Modules.Events.Domain;
using MarketingAutomation.Modules.Events.Infrastructure;
using MarketingAutomation.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Events.Application;

public sealed record EventInput(
    string Name,
    IdentifierType IdentifierType,
    string IdentifierValue,
    DateTimeOffset? OccurredAt = null,
    Dictionary<string, object?>? Properties = null,
    string? MessageId = null);

/// <summary>
/// Ingests a batch of events idempotently. Within-batch and against-store duplicates
/// (by MessageId) are dropped so retried deliveries never double-count. Returns how
/// many were newly stored vs. skipped.
/// </summary>
public sealed record IngestEventsCommand(IReadOnlyList<EventInput> Events) : IRequest<IngestResult>;

public sealed record IngestResult(int Accepted, int Duplicates);

public sealed class IngestEventsValidator : AbstractValidator<IngestEventsCommand>
{
    public IngestEventsValidator()
    {
        RuleFor(c => c.Events).NotEmpty();
        RuleForEach(c => c.Events).ChildRules(e =>
        {
            e.RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            e.RuleFor(x => x.IdentifierValue).NotEmpty();
        });
    }
}

public sealed class IngestEventsHandler(EventsDbContext db, ITenantContext tenantContext)
    : IRequestHandler<IngestEventsCommand, IngestResult>
{
    public async Task<IngestResult> Handle(IngestEventsCommand request, CancellationToken ct)
    {
        // Deduplicate within the batch first.
        var seen = new HashSet<string>();
        var candidates = new List<EventInput>();
        var withinBatchDupes = 0;
        foreach (var input in request.Events)
        {
            if (input.MessageId is not null && !seen.Add(input.MessageId))
            {
                withinBatchDupes++;
                continue;
            }
            candidates.Add(input);
        }

        // Then drop any whose MessageId already exists in the store.
        var messageIds = candidates.Where(c => c.MessageId is not null).Select(c => c.MessageId!).ToList();
        var existing = messageIds.Count == 0
            ? new HashSet<string>()
            : (await db.Events
                .Where(e => e.MessageId != null && messageIds.Contains(e.MessageId))
                .Select(e => e.MessageId!)
                .ToListAsync(ct)).ToHashSet();

        var accepted = 0;
        foreach (var input in candidates)
        {
            if (input.MessageId is not null && existing.Contains(input.MessageId)) continue;

            var stored = new StoredEvent
            {
                Name = input.Name,
                IdentifierType = input.IdentifierType,
                IdentifierValue = input.IdentifierValue,
                OccurredAt = input.OccurredAt ?? DateTimeOffset.UtcNow,
                Properties = input.Properties ?? new(),
                MessageId = input.MessageId,
            };
            stored.RaiseIntegrationEvent(new EventIngested(
                Guid.CreateVersion7(), tenantContext.TenantId, stored.OccurredAt,
                stored.Id, stored.Name, stored.IdentifierType, stored.IdentifierValue));

            db.Events.Add(stored);
            accepted++;
        }

        await db.SaveChangesAsync(ct);
        return new IngestResult(accepted, withinBatchDupes + (candidates.Count - accepted));
    }
}
