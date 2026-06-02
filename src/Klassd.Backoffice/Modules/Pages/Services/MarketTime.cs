namespace Klassd.Backoffice.Modules.Pages.Services;

/// <summary>
/// Converts between a market's wall-clock time (what editors author) and UTC (what is stored and
/// compared). Schedule times are entered as local time in the page's market time zone, so "00:00"
/// means local midnight in that market regardless of the editor's own location.
/// </summary>
public static class MarketTime
{
    /// <summary>Market wall-clock → UTC. Nudges out of a spring-forward gap so invalid times don't throw.</summary>
    public static DateTime ToUtc(DateTime marketWallClock, TimeZoneInfo tz)
    {
        var unspecified = DateTime.SpecifyKind(marketWallClock, DateTimeKind.Unspecified);
        if (tz.IsInvalidTime(unspecified))
            unspecified = unspecified.AddHours(1); // DST gap (e.g. 02:30 that doesn't exist) → next valid instant
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }

    /// <summary>UTC → market wall-clock (for display in the editor).</summary>
    public static DateTime ToLocal(DateTime utc, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

    /// <summary>A short label for a zone at a given instant, e.g. "Europe/Berlin (UTC+01:00)".</summary>
    public static string Label(TimeZoneInfo tz, DateTime atUtc)
    {
        var off = tz.GetUtcOffset(DateTime.SpecifyKind(atUtc, DateTimeKind.Utc));
        var sign = off < TimeSpan.Zero ? "-" : "+";
        var abs = off.Duration();
        return $"{tz.Id} (UTC{sign}{abs.Hours:00}:{abs.Minutes:00})";
    }
}
