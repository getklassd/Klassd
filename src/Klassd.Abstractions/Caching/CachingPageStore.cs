using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;

namespace Klassd.Abstractions.Caching;

/// <summary>
/// Cache-agnostic read-through decorator over an <see cref="IPageStore"/>. Caches the
/// read methods and invalidates the whole <c>page:</c> namespace on any write — wholesale
/// invalidation keeps it correct given page relationships (cascade slug renames,
/// parent/child, locale lists). Slug-uniqueness lookups (<see cref="FindBySlugAsync"/>)
/// are never cached, so writes always validate against fresh data. The actual cache
/// backend is supplied via <see cref="ICmsCache"/> (in-memory, Redis, …).
/// </summary>
public sealed class CachingPageStore(IPageStore inner, ICmsCache cache, CmsCacheOptions options) : IPageStore
{
    private const string Prefix = "page:";
    private static string IdKey(string id) => $"{Prefix}id:{id}";
    private static string LocaleKey(string locale) => $"{Prefix}locale:{locale}";
    private static string ContentKey(string contentId) => $"{Prefix}content:{contentId}";
    private static string ChildrenKey(string parentId, string locale) => $"{Prefix}children:{parentId}:{locale}";

    private Task Invalidate(CancellationToken ct) => cache.RemoveByPrefixAsync(Prefix, ct);

    // ── Reads (cached) ────────────────────────────────────────────────
    public Task<PageRecord?> GetByIdAsync(string id, CancellationToken ct = default) =>
        cache.GetOrCreateAsync(IdKey(id), c => inner.GetByIdAsync(id, c), options.Ttl, ct);

    public Task<IReadOnlyList<PageRecord>> GetByLocaleAsync(string localeCode, CancellationToken ct = default) =>
        CachedList(LocaleKey(localeCode), c => inner.GetByLocaleAsync(localeCode, c), ct);

    public Task<IReadOnlyList<PageRecord>> GetByContentIdAsync(string contentId, CancellationToken ct = default) =>
        CachedList(ContentKey(contentId), c => inner.GetByContentIdAsync(contentId, c), ct);

    public Task<IReadOnlyList<PageRecord>> GetChildrenAsync(string parentId, string localeCode, CancellationToken ct = default) =>
        CachedList(ChildrenKey(parentId, localeCode), c => inner.GetChildrenAsync(parentId, localeCode, c), ct);

    private async Task<IReadOnlyList<PageRecord>> CachedList(
        string key, Func<CancellationToken, Task<IReadOnlyList<PageRecord>>> load, CancellationToken ct)
    {
        var cached = await cache.GetOrCreateAsync<PageRecord[]>(
            key, async c => (await load(c)).ToArray(), options.Ttl, ct);
        return cached ?? [];
    }

    // ── Uniqueness lookup (never cached — must be fresh) ──────────────
    public Task<PageRecord?> FindBySlugAsync(string localeCode, string slug, string? excludeId, CancellationToken ct = default) =>
        inner.FindBySlugAsync(localeCode, slug, excludeId, ct);

    // ── Writes (invalidate) ───────────────────────────────────────────
    public async Task InsertAsync(PageRecord page, CancellationToken ct = default)
    {
        await inner.InsertAsync(page, ct);
        await Invalidate(ct);
    }

    public async Task<PageRecord?> ReplaceAsync(PageRecord page, CancellationToken ct = default)
    {
        var result = await inner.ReplaceAsync(page, ct);
        await Invalidate(ct);
        return result;
    }

    public async Task UpdateSlugAsync(string id, string slug, DateTime updatedAt, CancellationToken ct = default)
    {
        await inner.UpdateSlugAsync(id, slug, updatedAt, ct);
        await Invalidate(ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var deleted = await inner.DeleteAsync(id, ct);
        await Invalidate(ct);
        return deleted;
    }
}
