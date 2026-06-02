using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Klassd.Core.Localization;

namespace Klassd.Backoffice.Modules.Dictionary.Services;

/// <summary>
/// Translation dictionary logic over <see cref="IDictionaryStore"/>. Resolves a per-locale key→value
/// map using the locale fallback chain so missing translations degrade gracefully.
/// </summary>
public sealed class DictionaryService(IDictionaryStore store, LocaleRegistry locales)
{
    public Task<IReadOnlyList<DictionaryEntryRecord>> GetAllAsync(CancellationToken ct = default) =>
        store.GetAllAsync(ct);

    public Task<DictionaryEntryRecord?> GetAsync(string key, CancellationToken ct = default) =>
        store.GetAsync(key, ct);

    /// <summary>Inserts/replaces an entry. Empty values are dropped so fallback stays meaningful.</summary>
    public Task UpsertAsync(string key, IDictionary<string, string> values, CancellationToken ct = default)
    {
        key = key.Trim();
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Dictionary key is required.");

        var clean = values
            .Where(v => !string.IsNullOrEmpty(v.Value))
            .ToDictionary(v => v.Key, v => v.Value);
        return store.UpsertAsync(new DictionaryEntryRecord { Key = key, Values = clean }, ct);
    }

    public Task<bool> DeleteAsync(string key, CancellationToken ct = default) =>
        store.DeleteAsync(key, ct);

    /// <summary>
    /// The resolved key→value map for a locale: each value is taken from the first locale in the
    /// fallback chain (e.g. <c>en-dk → en</c>) that has a non-empty translation. Keys with no value
    /// anywhere in the chain are omitted.
    /// </summary>
    public async Task<Dictionary<string, string>> ResolveAsync(string localeCode, CancellationToken ct = default)
    {
        var chain = locales.GetFallbackChain(localeCode);
        var entries = await store.GetAllAsync(ct);

        var map = new Dictionary<string, string>();
        foreach (var entry in entries)
        {
            foreach (var code in chain)
            {
                if (entry.Values.TryGetValue(code, out var value) && !string.IsNullOrEmpty(value))
                {
                    map[entry.Key] = value;
                    break;
                }
            }
        }
        return map;
    }
}
