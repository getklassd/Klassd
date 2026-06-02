namespace Klassd.Core.Localization;

/// <param name="TimeZone">
/// IANA time zone id for this market (e.g. <c>Europe/Berlin</c>, <c>Asia/Dubai</c>). Content schedule
/// times are authored as wall-clock time in this zone — "00:00" means local midnight in the market.
/// Null ⇒ UTC.
/// </param>
public record LocaleDefinition(string Code, string? FallbackTo = null, bool Mandatory = false, bool IsDefault = false, string? TimeZone = null);

// Used when loading locales from appsettings.json
public record LocaleConfig(string Code, string? FallbackTo = null, bool Mandatory = false, bool IsDefault = false, string? TimeZone = null);
