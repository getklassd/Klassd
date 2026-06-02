namespace Klassd.Core.Localization;

public class LocaleRegistry(IEnumerable<LocaleDefinition> locales)
{
    public IReadOnlyList<LocaleDefinition> All { get; } = locales.ToList();

    /// <summary>Admin display for a locale code: "Label (code)" when labelled, else the bare code.</summary>
    public string DisplayLabel(string code) =>
        All.FirstOrDefault(l => l.Code == code)?.DisplayLabel ?? code.ToLowerInvariant();

    /// <summary>
    /// The market time zone for a locale (its <see cref="LocaleDefinition.TimeZone"/>), or UTC if unset
    /// or unrecognized. Schedule wall-clock times are authored in this zone.
    /// </summary>
    public TimeZoneInfo TimeZoneFor(string code)
    {
        var tzId = All.FirstOrDefault(l => l.Code == code)?.TimeZone;
        if (string.IsNullOrWhiteSpace(tzId)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    /// <summary>
    /// Configured locale time zones that cannot be resolved on this machine (typically missing OS tz
    /// data in a slim container) and would therefore silently fall back to UTC. Used for a startup
    /// warning so misconfiguration is caught loudly instead of producing offset-wrong scheduling.
    /// </summary>
    public IReadOnlyList<(string Code, string TimeZone)> UnresolvedTimeZones()
    {
        var unresolved = new List<(string, string)>();
        foreach (var locale in All)
        {
            if (string.IsNullOrWhiteSpace(locale.TimeZone)) continue;
            try { _ = TimeZoneInfo.FindSystemTimeZoneById(locale.TimeZone); }
            catch (TimeZoneNotFoundException) { unresolved.Add((locale.Code, locale.TimeZone)); }
            catch (InvalidTimeZoneException) { unresolved.Add((locale.Code, locale.TimeZone)); }
        }
        return unresolved;
    }

    /// <summary>
    /// Returns the full fallback chain for a locale code, most-specific first.
    /// e.g. "en-dk" → ["en-dk", "en"]
    /// </summary>
    public IReadOnlyList<string> GetFallbackChain(string code)
    {
        var chain = new List<string> { code };
        var lookup = All.ToDictionary(l => l.Code);
        var current = lookup.GetValueOrDefault(code);
        while (current?.FallbackTo is { } fallback && !chain.Contains(fallback))
        {
            chain.Add(fallback);
            current = lookup.GetValueOrDefault(fallback);
        }
        return chain;
    }
}
