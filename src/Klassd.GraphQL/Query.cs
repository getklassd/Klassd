using HotChocolate;
using Klassd.Backoffice.Modules.Globals.Services;
using Klassd.Backoffice.Modules.Pages.Services;
using Klassd.Core.Localization;

namespace Klassd.GraphQL;

/// <summary>
/// Read-only GraphQL delivery, mirroring the REST <c>/api</c> endpoints: only live (published +
/// in-window) pages are returned. Resolved at request time, above the read-through cache.
/// </summary>
public sealed class Query
{
    /// <summary>Live pages for a locale (defaults to the primary/mandatory locale).</summary>
    public async Task<IReadOnlyList<PageNode>> GetPages(string? locale, [Service] PageService pages, [Service] LocaleRegistry locales)
    {
        var live = PageDelivery.ProjectLive(await pages.GetByLocaleAsync(Locale(locale, locales)), DateTime.UtcNow);
        return live.Select(GraphMap.ToNode).ToList();
    }

    /// <summary>A single live page by id, or null.</summary>
    public async Task<PageNode?> GetPage(string id, [Service] PageService pages)
    {
        var now = DateTime.UtcNow;
        var page = await pages.GetByIdAsync(id);
        return page is null || !PageSchedule.IsLive(page, now) ? null : GraphMap.ToNode(PageDelivery.Project(page, now));
    }

    /// <summary>A single live page by its (locale-unique) slug, or null.</summary>
    public async Task<PageNode?> GetPageBySlug(string slug, string? locale, [Service] PageService pages, [Service] LocaleRegistry locales)
    {
        var now = DateTime.UtcNow;
        var page = await pages.GetBySlugAsync(Locale(locale, locales), slug);
        return page is null || !PageSchedule.IsLive(page, now) ? null : GraphMap.ToNode(PageDelivery.Project(page, now));
    }

    /// <summary>All live translations of the page's content (across locales).</summary>
    public async Task<IReadOnlyList<PageNode>> GetPageTranslations(string id, [Service] PageService pages)
    {
        var page = await pages.GetByIdAsync(id);
        if (page is null) return [];
        var live = PageDelivery.ProjectLive(await pages.GetByContentIdAsync(page.ContentId), DateTime.UtcNow);
        return live.Select(GraphMap.ToNode).ToList();
    }

    /// <summary>A global singleton's content for a locale, or null.</summary>
    public async Task<GlobalNode?> GetGlobal(string name, string? locale, [Service] GlobalService globals, [Service] LocaleRegistry locales)
    {
        var record = await globals.GetForDeliveryAsync(name, Locale(locale, locales));
        return record is null ? null : GraphMap.ToNode(record);
    }

    /// <summary>Configured locales.</summary>
    public IReadOnlyList<LocaleNode> GetLocales([Service] LocaleRegistry locales) =>
        locales.All.Select(l => new LocaleNode(l.Code, l.Mandatory, l.IsDefault, l.FallbackTo)).ToList();

    private static string Locale(string? requested, LocaleRegistry locales) =>
        requested ?? locales.All.FirstOrDefault(l => l.Mandatory)?.Code ?? locales.All.FirstOrDefault()?.Code ?? "en";
}
