using System.Globalization;

namespace Klassd.Backoffice.Modules.Pages.Services;

/// <summary>
/// Resolves the instant block scheduling is evaluated at for a delivery request. Normally "now",
/// but when preview is enabled a caller may pass <c>?preview=&lt;datetime&gt;</c> to time-travel
/// (see future or expired content). Preview is gated by config so it can be disabled in production.
/// </summary>
public static class SchedulePreview
{
    /// <summary>
    /// Returns the preview instant when <paramref name="enabled"/> and <paramref name="rawPreview"/> parses
    /// as a date/time (no offset ⇒ treated as UTC); otherwise <paramref name="nowUtc"/>.
    /// </summary>
    public static DateTime Resolve(bool enabled, string? rawPreview, DateTime nowUtc)
    {
        if (!enabled || string.IsNullOrWhiteSpace(rawPreview))
            return nowUtc;

        return DateTimeOffset.TryParse(
            rawPreview, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed.UtcDateTime
            : nowUtc;
    }
}
