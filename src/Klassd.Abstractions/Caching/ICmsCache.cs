namespace Klassd.Abstractions.Caching;

/// <summary>
/// Cache abstraction used by the (optional) caching layer. Implement this to plug in
/// your own backend (Redis, distributed cache, …) and register it as <c>ICmsCache</c>
/// before calling <c>AddCaching()</c>. A built-in in-memory implementation is the default.
/// Implementations must be thread-safe and may be singletons.
/// </summary>
public interface ICmsCache
{
    /// <summary>Returns the cached value, or null on miss/expiry.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>Stores a value with an optional time-to-live.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class;

    /// <summary>Removes a single entry.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Removes every entry whose key starts with <paramref name="prefix"/> (namespace invalidation).</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or invokes <paramref name="factory"/>
    /// once on a miss and caches its non-null result. Backends with native get-or-create
    /// (e.g. HybridCache) override this for stampede protection and L1 promotion on every node;
    /// the default simply composes <see cref="GetAsync"/> and <see cref="SetAsync"/>.
    /// </summary>
    async Task<T?> GetOrCreateAsync<T>(
        string key, Func<CancellationToken, Task<T?>> factory, TimeSpan? ttl = null, CancellationToken ct = default)
        where T : class
    {
        if (await GetAsync<T>(key, ct) is { } hit) return hit;
        var created = await factory(ct);
        if (created is not null) await SetAsync(key, created, ttl, ct);
        return created;
    }
}
