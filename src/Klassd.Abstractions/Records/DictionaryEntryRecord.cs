namespace Klassd.Abstractions.Records;

/// <summary>
/// A dictionary item: a flat string <see cref="Key"/> (e.g. "common.no") with one translation per
/// locale code in <see cref="Values"/> (e.g. {"en":"No","da-dk":"Nej","de":"Nein"}). The frontend
/// fetches a per-locale map (resolved through the locale fallback chain).
/// </summary>
public sealed class DictionaryEntryRecord
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Translation per locale code. Missing/empty values fall back via the locale chain at delivery.</summary>
    public Dictionary<string, string> Values { get; set; } = new();
}
