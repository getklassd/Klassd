using Klassd.Abstractions.Media;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Search;
using Klassd.Abstractions.Storage;
using Klassd.Backoffice.Modules.Pages.Services;
using Klassd.Backoffice.State;

namespace Klassd.Backoffice.Modules.Search;

/// <summary>One search hit. <see cref="Kind"/> is "page" or "media"; <see cref="Href"/> is an admin route.</summary>
public sealed record SearchResult(string Kind, string Title, string Subtitle, string Href, string? Tag);

/// <summary>
/// Admin search over pages + media (all sections). Pages are served by a registered
/// <see cref="ICmsSearchIndex"/> (e.g. Lucene — tokenized + ranked) when present, otherwise by the
/// built-in case-insensitive substring scan (page Name/Slug + any [Indexable] data value, see
/// <see cref="SearchableFields.PageFields"/>). Media is always a substring scan (FileName/DisplayName/
/// AltText) — it isn't indexed yet. The substring scan is O(n), fine at admin content scale.
/// </summary>
public sealed class SearchService(
    PageService pages, MediaService media, LocaleState locale, SearchableFields searchable,
    IEnumerable<ICmsSearchIndex> searchIndexes)
{
    private const int Cap = 50;
    private readonly ICmsSearchIndex? _index = searchIndexes.FirstOrDefault();

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string? query, CancellationToken ct = default)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q)) return [];

        var results = new List<SearchResult>();

        await locale.EnsureLoadedAsync();
        var localeCode = !string.IsNullOrEmpty(locale.SelectedLocale)
            ? locale.SelectedLocale
            : locale.DefaultLocale?.Code ?? "en";

        if (_index is not null)
        {
            // Tokenized + ranked page hits from the search index (current locale).
            foreach (var hit in await _index.SearchAsync(q, localeCode, Cap, ct))
                if (hit.Kind == "page")
                    results.Add(new SearchResult("page", hit.Title, hit.Subtitle ?? "", hit.Href ?? "", hit.Tag));
        }
        else
        {
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
