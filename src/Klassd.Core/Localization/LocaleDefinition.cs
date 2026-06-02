namespace Klassd.Core.Localization;

/// <param name="TimeZone">
/// IANA time zone id for this market (e.g. <c>Europe/Berlin</c>, <c>Asia/Dubai</c>). Content schedule
/// times are authored as wall-clock time in this zone — "00:00" means local midnight in the market.
/// Null ⇒ UTC.
/// </param>
/// <param name="Label">
/// Optional human-friendly name shown in the admin (e.g. "English / Denmark"). When set, the UI
/// renders "<c>Label (code)</c>"; otherwise it falls back to the bare code.
/// </param>
public record LocaleDefinition(string Code, string? FallbackTo = null, bool Mandatory = false, bool IsDefault = false, string? TimeZone = null, string? Label = null)
{
    /// <summary>How this locale reads in the admin: "Label (en-dk)" when a label is set, else "en-dk".</summary>
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(Label) ? Code.ToLowerInvariant() : $"{Label} ({Code.ToLowerInvariant()})";
}

// Used when loading locales from appsettings.json
public record LocaleConfig(string Code, string? FallbackTo = null, bool Mandatory = false, bool IsDefault = false, string? TimeZone = null, string? Label = null);
