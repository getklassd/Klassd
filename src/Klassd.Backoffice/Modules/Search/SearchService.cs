using Klassd.Abstractions.Media;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Klassd.Backoffice.Modules.Pages.Services;
using Klassd.Backoffice.State;

namespace Klassd.Backoffice.Modules.Search;

/// <summary>One search hit. <see cref="Kind"/> is "page" or "media"; <see cref="Href"/> is an admin route.</summary>
public sealed record SearchResult(string Kind, string Title, string Subtitle, string Href, string? Tag);

/// <summary>
/// In-memory admin search over pages (current/default locale) + media (all sections). Matches the
/// always-present built-ins (page Name/Slug, media FileName/DisplayName/AltText) plus any page data
/// value whose key is [Indexable] (<see cref="SearchableFields.PageFields"/>). Case-insensitive substring.
///
/// O(n) in-process scan — fine at admin content scale; the [Indexable] DB indexes exist so a future
/// SQL-backed search can replace this without touching callers.
/// </summary>
public sealed class SearchService(
    PageService pages, MediaService media, LocaleState locale, SearchableFields searchable)
{
    private const int Cap = 50;

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string? query, CancellationToken ct = default)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q)) return [];

        var results = new List<SearchResult>();

        await locale.EnsureLoadedAsync();
        var localeCode = !string.IsNullOrEmpty(locale.SelectedLocale)
            ? locale.SelectedLocale
            : locale.DefaultLocale?.Code ?? "en";

        foreach (var p in await pages.GetByLocaleAsync(localeCode))
        {
            if (Has(p.Name, q) || Has(p.Slug, q) || MatchesIndexable(p, q))
                results.Add(new SearchResult(
                    "page",
                    string.IsNullOrWhiteSpace(p.Name) ? p.Slug : p.Name,
                    "/" + p.Slug,
                    $"/admin/pages?edit={p.Id}",
                    p.PageTypeName));
        }

        foreach (var section in media.Sections)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var m in await media.ListAsync(section.Name, ct))
            {
                if (Has(m.FileName, q) || Has(m.DisplayName, q) || Has(m.AltText, q))
                    results.Add(new SearchResult(
                        "media",
                        string.IsNullOrWhiteSpace(m.DisplayName) ? m.FileName : m.DisplayName!,
                        m.FileName,
                        $"/admin/media/{m.Section}",
                        m.Section));
            }
        }

        return results
            .GroupBy(r => (r.Kind, r.Href, r.Title))
            .Select(g => g.First())
            .Take(Cap)
            .ToList();
    }

    private bool MatchesIndexable(PageRecord p, string q)
    {
        foreach (var key in searchable.PageFields)
            if (p.Data.TryGetValue(key, out var v) && Has(v, q))
                return true;
        return false;
    }

    private static bool Has(string? v, string q) =>
        !string.IsNullOrEmpty(v) && v.Contains(q, StringComparison.OrdinalIgnoreCase);
}
