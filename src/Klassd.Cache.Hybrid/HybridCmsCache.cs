using Klassd.Abstractions.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace Klassd.Cache.Hybrid;

/// <summary>
/// Adapts Microsoft's <see cref="HybridCache"/> (L1 in-process + an optional L2
/// <c>IDistributedCache</c>) to <see cref="ICmsCache"/>. HybridCache supplies stampede
/// protection and a pluggable serializer / distributed backend, so this type only bridges
/// the two API shapes:
/// <list type="bullet">
/// <item><see cref="ICmsCache.GetAsync"/> is read-only (null on miss) whereas HybridCache is
/// get-or-create — we probe with a null-returning factory and writes disabled so a miss is
/// never cached.</item>
/// <item><see cref="ICmsCache.RemoveByPrefixAsync"/> is mapped onto HybridCache tag
/// invalidation: every entry is tagged with its first <c>:</c>-delimited segment (its
/// namespace), and a prefix is resolved to that tag. The CMS page cache only ever clears the
/// whole <c>page:</c> namespace, so this is exact for its usage.</item>
/// </list>
/// </summary>
public sealed class HybridCmsCache(HybridCache cache, CmsCacheOptions options) : ICmsCache
{
    // Read-only probe: disabling both writes means an L1/L2 miss (factory → null) is never
    // written back, so GetAsync never poisons the cache with nulls.
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite | HybridCacheEntryFlags.DisableDistributedCacheWrite,
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class =>
        await cache.GetOrCreateAsync<T?>(
            key,
            static _ => ValueTask.FromResult<T?>(null),
            ProbeOptions,
            cancellationToken: ct);

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class
    {
        var local = options.LocalTtl ?? ttl;
        var entry = ttl is not null || local is not null
            ? new HybridCacheEntryOptions { Expiration = ttl, LocalCacheExpiration = local }
            : null;
        await cache.SetAsync(key, value, entry, Tags(key), ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default) =>
        await cache.RemoveAsync(key, ct);

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) =>
        await cache.RemoveByTagAsync(Namespace(prefix), ct);

    /// <summary>
    /// Native get-or-create: HybridCache promotes an L2 hit into this node's L1 and dedupes
    /// concurrent factory calls (stampede protection), which the default get-then-set cannot.
    /// </summary>
    public async Task<T?> GetOrCreateAsync<T>(
        string key, Func<CancellationToken, Task<T?>> factory, TimeSpan? ttl = null, CancellationToken ct = default)
        where T : class
    {
        var local = options.LocalTtl ?? ttl;
        var entry = ttl is not null || local is not null
            ? new HybridCacheEntryOptions { Expiration = ttl, LocalCacheExpiration = local }
            : null;

        return await cache.GetOrCreateAsync(
            key,
            factory,
            static async (f, c) => await f(c),
            entry,
            Tags(key),
            ct);
    }

    private static string[] Tags(string key) => [Namespace(key)];

    /// <summary>The first <c>:</c>-delimited segment of a key/prefix (e.g. <c>page:id:42</c> → <c>page</c>).</summary>
    private static string Namespace(string keyOrPrefix)
    {
        var i = keyOrPrefix.IndexOf(':');
        return i < 0 ? keyOrPrefix : keyOrPrefix[..i];
    }
}
