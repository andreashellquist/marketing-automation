namespace MarketingAutomation.Modules.Messaging.Domain;

public static class QuietHours
{
    /// <summary>
    /// True if <paramref name="instant"/> falls inside the tenant's quiet window in the
    /// recipient's local time. Windows may wrap midnight (e.g. 21→08). An unknown timezone
    /// falls back to UTC rather than risking an off-hours send under the wrong assumption.
    /// </summary>
    public static bool IsWithin(DateTimeOffset instant, string? recipientTimezone, int startHour, int endHour)
    {
        var tz = ResolveTimeZone(recipientTimezone);
        var localHour = TimeZoneInfo.ConvertTime(instant, tz).Hour;

        return startHour <= endHour
            ? localHour >= startHour && localHour < endHour
            : localHour >= startHour || localHour < endHour; // wraps midnight
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
