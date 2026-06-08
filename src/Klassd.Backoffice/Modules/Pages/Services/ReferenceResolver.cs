using Klassd.Abstractions.Media;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Core.Localization;
using Klassd.Core.Services;

namespace Klassd.Backoffice.Modules.Pages.Services;

/// <summary>
/// Resolves a page's <c>PageReference</c>/<c>MediaReference</c> fields into URLs for headless
/// delivery. A page reference stores the target's ContentId; a media reference stores the media id.
/// At <c>depth &lt;= 0</c> nothing is resolved (the raw ids stay in <c>data</c>). One level is resolved
/// today; deeper nesting is a future extension.
/// </summary>
public sealed class ReferenceResolver(
    IPageStore pages, IMediaStore media, PageTypeRegistry pageTypes, LocaleRegistry locales)
{
    public async Task<DeliveredPage> ResolveAsync(
        PageRecord page, int depth, IReadOnlySet<string>? expand, CancellationToken ct = default)
    {
        if (depth <= 0)
            return DeliveredPage.From(page, null);

        var type = pageTypes.Get(page.PageTypeName);
        if (type is null)
            return DeliveredPage.From(page, null);

        var map = new Dictionary<string, ResolvedReference>();
        foreach (var field in type.Fields)
        {
            if (field.FieldType is not ("relationship" or "media")) continue;
            if (expand is not null && !expand.Contains(field.Name)) continue;
            if (!page.Data.TryGetValue(field.Name, out var value) || string.IsNullOrEmpty(value)) continue;

            var resolved = field.FieldType == "media"
                ? await ResolveMediaAsync(value, ct)
                : await ResolvePageAsync(value, page.LocaleCode, ct);
            if (resolved is not null) map[field.Name] = resolved;
        }

        return DeliveredPage.From(page, map.Count > 0 ? map : null);
    }

    private async Task<ResolvedReference?> ResolveMediaAsync(string mediaId, CancellationToken ct)
    {
        var m = await media.GetAsync(mediaId, ct);
        if (m is null) return null;
        var title = string.IsNullOrWhiteSpace(m.DisplayName) ? m.FileName : m.DisplayName;
        return new ResolvedReference("media", m.Id, $"/api/media/{m.Id}", Slug: null, title, m.AltText);
    }

    private async Task<ResolvedReference?> ResolvePageAsync(string contentId, string preferredLocale, CancellationToken ct)
    {
        var translations = await pages.GetByContentIdAsync(contentId, ct);
        if (translations.Count == 0) return null;
        // Prefer the referrer's locale so links stay within the same market; else any translation.
        var target = translations.FirstOrDefault(p => p.LocaleCode == preferredLocale) ?? translations[0];
        return new ResolvedReference("page", target.Id, PublicPath(target.Slug, target.LocaleCode), target.Slug, target.Name, AltText: null);
    }

    private string PublicPath(string slug, string locale)
    {
        var primary = locales.All.FirstOrDefault(l => l.Mandatory)?.Code
                      ?? locales.All.FirstOrDefault(l => l.IsDefault)?.Code
                      ?? locales.All.FirstOrDefault()?.Code;
        var prefix = locale == primary ? string.Empty : locale + "/";
        return "/" + prefix + slug;
    }
}
