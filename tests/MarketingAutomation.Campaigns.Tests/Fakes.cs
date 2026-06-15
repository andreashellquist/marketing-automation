using System.Runtime.CompilerServices;
using MarketingAutomation.SharedKernel.Contracts;

namespace MarketingAutomation.Campaigns.Tests;

public sealed class FakeAudienceResolver : IAudienceResolver
{
    public List<AudienceMember> Members { get; } = [];

    public async IAsyncEnumerable<AudienceMember> ResolveAsync(
        Guid? segmentId, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var member in Members)
        {
            await Task.Yield();
            yield return member;
        }
    }
}

public sealed class FakeMessageSender : IMessageSender
{
    public List<SendRequest> Sent { get; } = [];
    public Task SendAsync(SendRequest request, CancellationToken ct)
    {
        Sent.Add(request);
        return Task.CompletedTask;
    }
}
