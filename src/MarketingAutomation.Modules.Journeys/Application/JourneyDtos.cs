using MarketingAutomation.Modules.Journeys.Domain;
using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Journeys.Application;

public sealed record JourneyDto(
    Guid Id, string Name, JourneyStatus Status, int Version, Channel Channel,
    ReentryPolicy ReentryPolicy, string? StartNodeId, IReadOnlyList<JourneyNode> Nodes, DateTimeOffset CreatedAt)
{
    public static JourneyDto From(Journey j) => new(
        j.Id, j.Name, j.Status, j.Version, j.Channel, j.ReentryPolicy, j.StartNodeId, j.Nodes, j.CreatedAt);
}

public sealed record EnrollmentResultDto(Guid? RunId, JourneyRunStatus? Status, string Outcome);
