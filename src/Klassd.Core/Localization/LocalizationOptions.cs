namespace Klassd.Core.Localization;

/// <summary>
/// Configures the set of locales (codes, fallbacks, mandatory/default flags).
/// Which page types and properties are localized is declared with the
/// <c>[LocalizedPage]</c> / <c>[Localized]</c> attributes, not here.
/// </summary>
public class LocalizationOptions
{
    public List<LocaleDefinition> Locales { get; } = [];

    // ── Locale configuration ──────────────────────────────────────

    public LocalizationOptions AddLocale(string code, Action<LocaleBuilder>? configure = null)
    {
        var builder = new LocaleBuilder(code);
        configure?.Invoke(builder);
        Locales.RemoveAll(l => l.Code == code); // allow override
        Locales.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Load locales from config (e.g. appsettings.json). Overrides any existing
    /// entry with the same code, allowing ops-level locale management without code changes.
    /// </summary>
    public LocalizationOptions LoadFrom(IEnumerable<LocaleConfig>? configs)
    {
        if (configs is null) return this;
        foreach (var c in configs)
            AddLocale(c.Code, l =>
            {
                if (c.Mandatory) l.AsMandatory();
                if (c.IsDefault) l.AsDefault();
                if (c.FallbackTo is not null) l.WithFallback(c.FallbackTo);
                if (c.TimeZone is not null) l.WithTimeZone(c.TimeZone);
                if (c.Label is not null) l.WithLabel(c.Label);
            });
        return this;
    }
}

public class LocaleBuilder(string code)
{
    private string? _fallbackTo;
    private bool _mandatory;
    private bool _isDefault;
    private string? _timeZone;
    private string? _label;

    public LocaleBuilder WithFallback(string localeCode) { _fallbackTo = localeCode; return this; }
    public LocaleBuilder AsMandatory() { _mandatory = true; return this; }
    public LocaleBuilder AsDefault() { _isDefault = true; return this; }
    /// <summary>IANA time zone id for this market (e.g. "Europe/Berlin"), used to author schedule times.</summary>
    public LocaleBuilder WithTimeZone(string ianaId) { _timeZone = ianaId; return this; }
    /// <summary>Human-friendly name shown in the admin (e.g. "English / Denmark").</summary>
    public LocaleBuilder WithLabel(string label) { _label = label; return this; }

    internal LocaleDefinition Build() => new(code, _fallbackTo, _mandatory, _isDefault, _timeZone, _label);
}
