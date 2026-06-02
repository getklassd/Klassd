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
}
