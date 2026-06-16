using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Contracts;

namespace MarketingAutomation.Modules.Journeys.Domain;

/// <summary>
/// Advances a run through the graph until it must pause (a wait) or completes. This is the
/// whole state machine: it walks nodes, performs side effects (sends), and persists the
/// pause state on the run. It never blocks on real time — waits are recorded, not slept.
/// Resuming a paused run is the caller's job (the scheduler for time, event delivery for
/// events); both set the run back to Active on the correct successor before calling here.
/// </summary>
public sealed class JourneyRunner(IMessageSender messageSender)
{
    // Guards against a cycle with no wait/exit looping forever in one pass.
    private const int MaxStepsPerPass = 1000;

    public async Task AdvanceAsync(Journey journey, JourneyRun run, CancellationToken ct)
    {
        for (var step = 0; step < MaxStepsPerPass; step++)
        {
            var node = journey.FindNode(run.CurrentNodeId);
            if (node is null)
            {
                Complete(run);
                return;
            }

            switch (node.Type)
            {
                case JourneyNodeType.Exit:
                    Complete(run);
                    return;

                case JourneyNodeType.SendMessage:
                    await SendAsync(journey, run, node, ct);
                    run.CurrentNodeId = node.NextNodeId;
                    break;

                case JourneyNodeType.Split:
                    run.CurrentNodeId = PickSplit(node);
                    break;

                case JourneyNodeType.Wait:
                    run.Status = JourneyRunStatus.Waiting;
                    run.WaitEventName = null;
                    run.WakeUpAtTicks = DateTimeOffset.UtcNow.AddSeconds(node.WaitSeconds).UtcTicks;
                    return;

                case JourneyNodeType.WaitForEvent:
                    run.Status = JourneyRunStatus.Waiting;
                    run.WaitEventName = node.EventName;
                    run.WakeUpAtTicks = node.TimeoutSeconds > 0
                        ? DateTimeOffset.UtcNow.AddSeconds(node.TimeoutSeconds).UtcTicks
                        : null;
                    return;

                default:
                    throw new InvalidOperationException($"Unknown journey node type '{node.Type}'.");
            }
        }

        throw new InvalidOperationException(
            $"Journey '{journey.Id}' exceeded {MaxStepsPerPass} steps in one pass — likely a cycle without a wait or exit.");
    }

    private async Task SendAsync(Journey journey, JourneyRun run, JourneyNode node, CancellationToken ct)
    {
        await messageSender.SendAsync(new SendRequest(
            Channel: journey.Channel,
            Purpose: MessagePurpose.Marketing,
            Recipient: run.Recipient,
            Body: node.Body ?? "",
            // At-most-once per (run, node): a re-advance after a crash never double-sends.
            DedupKey: $"journey:{run.Id}:{node.Id}",
            Subject: node.Subject,
            RecipientTimezone: run.RecipientTimezone,
            ContactId: run.ContactId,
            SourceType: "journey",
            SourceId: journey.Id), ct);
    }

    private static string? PickSplit(JourneyNode node) =>
        Random.Shared.Next(100) < node.SplitPercent ? node.NextNodeIdB : node.NextNodeId;

    private static void Complete(JourneyRun run)
    {
        run.Status = JourneyRunStatus.Completed;
        run.CurrentNodeId = null;
        run.WakeUpAtTicks = null;
        run.WaitEventName = null;
    }
}
