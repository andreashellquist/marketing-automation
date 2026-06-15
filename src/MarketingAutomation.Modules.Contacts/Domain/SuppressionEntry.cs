using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Contacts.Domain;

/// <summary>
/// A normalized address (email/phone) that must never receive marketing on a channel.
/// Checked at send time across all channels, independent of contact-level consent.
/// </summary>
public sealed class SuppressionEntry : TenantEntity
{
    public Channel Channel { get; set; }

    /// <summary>Normalized identifier value (lowercased email / E.164 phone).</summary>
    public required string Value { get; set; }

    public SuppressionReason Reason { get; set; }
    public string? Notes { get; set; }
}
