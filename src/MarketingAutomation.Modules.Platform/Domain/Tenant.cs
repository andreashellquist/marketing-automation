using MarketingAutomation.SharedKernel;

namespace MarketingAutomation.Modules.Platform.Domain;

public sealed class Tenant : Entity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }

    /// <summary>Kill switch: when true, the send pipeline holds everything for this tenant.</summary>
    public bool SendingPaused { get; set; }

    /// <summary>Max marketing messages per contact per day across channels; null = uncapped.</summary>
    public int? MaxMarketingPerDay { get; set; }

    public bool QuietHoursEnabled { get; set; }
    public int QuietStartHour { get; set; } = 21;
    public int QuietEndHour { get; set; } = 8;
}
