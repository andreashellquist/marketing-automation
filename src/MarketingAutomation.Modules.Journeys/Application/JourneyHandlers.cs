using FluentValidation;
using MarketingAutomation.Modules.Journeys.Domain;
using MarketingAutomation.Modules.Journeys.Infrastructure;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MarketingAutomation.Modules.Journeys.Application;

// ---- Create / Activate / Get --------------------------------------------------------

public sealed record CreateJourneyCommand(
    string Name, Channel Channel, ReentryPolicy ReentryPolicy, string? StartNodeId, List<JourneyNode> Nodes)
    : IRequest<JourneyDto>;

public sealed class CreateJourneyValidator : AbstractValidator<CreateJourneyCommand>
{
    public CreateJourneyValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
}

public sealed class CreateJourneyHandler(JourneysDbContext db) : IRequestHandler<CreateJourneyCommand, JourneyDto>
{
    public async Task<JourneyDto> Handle(CreateJourneyCommand request, CancellationToken ct)
    {
        var journey = new Journey
        {
            Name = request.Name,
            Channel = request.Channel,
            ReentryPolicy = request.ReentryPolicy,
            StartNodeId = request.StartNodeId,
            Nodes = request.Nodes,
        };
        db.Journeys.Add(journey);
        await db.SaveChangesAsync(ct);
        return JourneyDto.From(journey);
    }
}

public sealed record ActivateJourneyCommand(Guid Id) : IRequest<JourneyDto>;

public sealed class ActivateJourneyHandler(JourneysDbContext db) : IRequestHandler<ActivateJourneyCommand, JourneyDto>
{
    public async Task<JourneyDto> Handle(ActivateJourneyCommand request, CancellationToken ct)
    {
        var journey = await Load(db, request.Id, ct);
        journey.Activate();
        await db.SaveChangesAsync(ct);
        return JourneyDto.From(journey);
    }

    internal static async Task<Journey> Load(JourneysDbContext db, Guid id, CancellationToken ct) =>
        await db.Journeys.FirstOrDefaultAsync(j => j.Id == id, ct) ?? throw new NotFoundException("Journey", id);
}

public sealed record GetJourneyQuery(Guid Id) : IRequest<JourneyDto>;

public sealed class GetJourneyHandler(JourneysDbContext db) : IRequestHandler<GetJourneyQuery, JourneyDto>
{
    public async Task<JourneyDto> Handle(GetJourneyQuery request, CancellationToken ct)
    {
        var journey = await db.Journeys.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.Id, ct)
            ?? throw new NotFoundException("Journey", request.Id);
        return JourneyDto.From(journey);
    }
}

// ---- Enroll a contact ---------------------------------------------------------------

public sealed record EnrollContactCommand(
    Guid JourneyId, Guid ContactId, string Recipient, string? RecipientTimezone)
    : IRequest<EnrollmentResultDto>;

public sealed class EnrollContactHandler(JourneysDbContext db, JourneyRunner runner)
    : IRequestHandler<EnrollContactCommand, EnrollmentResultDto>
{
    public async Task<EnrollmentResultDto> Handle(EnrollContactCommand request, CancellationToken ct)
    {
        var journey = await db.Journeys.FirstOrDefaultAsync(j => j.Id == request.JourneyId, ct)
            ?? throw new NotFoundException("Journey", request.JourneyId);

        if (journey.Status != JourneyStatus.Active)
            throw new DomainConflictException("Only active journeys accept enrollments.");

        if (journey.ReentryPolicy == ReentryPolicy.Never)
        {
            var alreadyIn = await db.JourneyRuns.AnyAsync(r =>
                r.JourneyId == journey.Id && r.ContactId == request.ContactId &&
                r.Status != JourneyRunStatus.Completed, ct);
            if (alreadyIn)
                return new EnrollmentResultDto(null, null, "already_enrolled");
        }

        var run = new JourneyRun
        {
            JourneyId = journey.Id,
            JourneyVersion = journey.Version,
            ContactId = request.ContactId,
            Recipient = request.Recipient,
            RecipientTimezone = request.RecipientTimezone,
            CurrentNodeId = journey.StartNodeId,
        };
        db.JourneyRuns.Add(run);

        await runner.AdvanceAsync(journey, run, ct);
        await db.SaveChangesAsync(ct);

        return new EnrollmentResultDto(run.Id, run.Status, "enrolled");
    }
}

// ---- Resume due time-waits (scheduler tick, tenant-scoped) ---------------------------

public sealed record AdvanceDueRunsCommand : IRequest<int>;

public sealed class AdvanceDueRunsHandler(JourneysDbContext db, JourneyRunner runner)
    : IRequestHandler<AdvanceDueRunsCommand, int>
{
    public async Task<int> Handle(AdvanceDueRunsCommand request, CancellationToken ct)
    {
        var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
        var due = await db.JourneyRuns
            .Where(r => r.Status == JourneyRunStatus.Waiting && r.WakeUpAtTicks != null && r.WakeUpAtTicks <= nowTicks)
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        var journeys = await LoadJourneys(db, due, ct);
        foreach (var run in due)
        {
            if (!journeys.TryGetValue(run.JourneyId, out var journey)) continue;
            var node = journey.FindNode(run.CurrentNodeId);
            // A time-wait resumes to its successor; a wait-for-event timeout takes the timeout branch.
            run.CurrentNodeId = node?.Type == JourneyNodeType.WaitForEvent
                ? node.TimeoutNodeId ?? node.NextNodeId
                : node?.NextNodeId;
            await ResumeAndAdvance(journey, run, runner, ct);
        }

        await db.SaveChangesAsync(ct);
        return due.Count;
    }

    internal static async Task<Dictionary<Guid, Journey>> LoadJourneys(
        JourneysDbContext db, List<JourneyRun> runs, CancellationToken ct)
    {
        var ids = runs.Select(r => r.JourneyId).Distinct().ToList();
        return await db.Journeys.Where(j => ids.Contains(j.Id)).ToDictionaryAsync(j => j.Id, ct);
    }

    internal static async Task ResumeAndAdvance(Journey journey, JourneyRun run, JourneyRunner runner, CancellationToken ct)
    {
        run.Status = JourneyRunStatus.Active;
        run.WaitEventName = null;
        run.WakeUpAtTicks = null;
        await runner.AdvanceAsync(journey, run, ct);
    }
}

// ---- Deliver an event to parked runs ------------------------------------------------

public sealed record DeliverEventCommand(Guid ContactId, string EventName) : IRequest<int>;

public sealed class DeliverEventHandler(JourneysDbContext db, JourneyRunner runner)
    : IRequestHandler<DeliverEventCommand, int>
{
    public async Task<int> Handle(DeliverEventCommand request, CancellationToken ct)
    {
        var parked = await db.JourneyRuns
            .Where(r => r.Status == JourneyRunStatus.Waiting &&
                        r.ContactId == request.ContactId && r.WaitEventName == request.EventName)
            .ToListAsync(ct);
        if (parked.Count == 0) return 0;

        var journeys = await AdvanceDueRunsHandler.LoadJourneys(db, parked, ct);
        foreach (var run in parked)
        {
            if (!journeys.TryGetValue(run.JourneyId, out var journey)) continue;
            var node = journey.FindNode(run.CurrentNodeId);
            run.CurrentNodeId = node?.NextNodeId; // event arrived → on-event branch
            await AdvanceDueRunsHandler.ResumeAndAdvance(journey, run, runner, ct);
        }

        await db.SaveChangesAsync(ct);
        return parked.Count;
    }
}
