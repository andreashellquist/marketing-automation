using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Platform.Domain;

public sealed class Tenant : Entity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }

    /// <summary>Kill switch: when true, the send pipeline drops everything for this tenant.</summary>
    public bool SendingPaused { get; set; }
}
