using MarketingAutomation.Modules.Messaging.Domain;
using MarketingAutomation.SharedKernel;
using MarketingAutomation.SharedKernel.Contracts;

namespace MarketingAutomation.Messaging.Tests;

public sealed class FakeSendingControl : ISendingControl
{
    public TenantSendingPolicy Policy { get; set; } = TenantSendingPolicy.Default;
    public Task<TenantSendingPolicy> GetPolicyAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(Policy);
}

public sealed class FakeSuppressionChecker : ISuppressionChecker
{
    public HashSet<string> Suppressed { get; } = new();
    public Task<bool> IsSuppressedAsync(Channel channel, string normalizedValue, CancellationToken ct) =>
        Task.FromResult(Suppressed.Contains(normalizedValue));
}

public sealed class FakeChannelSender(Channel channel) : IChannelSender
{
    public int SendCount { get; private set; }
    public bool ShouldFail { get; set; }
    public Channel Channel => channel;

    public Task<SendOutcome> SendAsync(OutboundMessage message, CancellationToken ct)
    {
        SendCount++;
        return Task.FromResult(ShouldFail
            ? SendOutcome.Fail("fake", "boom")
            : SendOutcome.Ok("fake", $"fake-{SendCount}"));
    }
}
