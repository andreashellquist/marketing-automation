namespace MarketingAutomation.SharedKernel.Contracts;

/// <summary>
/// Cross-module contracts consumed by the Messaging send pipeline. The owning modules
/// implement them (Platform: sending control; Contacts: suppression) so Messaging never
/// references another module directly — only SharedKernel.
/// </summary>

public sealed record TenantSendingPolicy(
    bool SendingPaused,
    int? MaxMarketingPerDay,
    bool QuietHoursEnabled,
    int QuietStartHour,
    int QuietEndHour)
{
    public static TenantSendingPolicy Default { get; } = new(false, null, false, 21, 8);
}

/// <summary>Tenant-level sending policy and kill switch (implemented by Platform).</summary>
public interface ISendingControl
{
    Task<TenantSendingPolicy> GetPolicyAsync(Guid tenantId, CancellationToken ct);
}

/// <summary>Send-time suppression lookup (implemented by Contacts), scoped to the ambient tenant.</summary>
public interface ISuppressionChecker
{
    Task<bool> IsSuppressedAsync(Channel channel, string normalizedValue, CancellationToken ct);
}
