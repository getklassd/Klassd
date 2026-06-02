using System.Collections.Concurrent;
using System.Text.Json;
using Klassd.Abstractions.Caching;

namespace Klassd.Cache.InMemory;

/// <summary>
/// In-process <see cref="ICmsCache"/>. Stores values as JSON so every read returns a
/// fresh copy (no shared-instance aliasing) and behavior matches a distributed cache.
/// Suitable for single-instance deployments; use the Redis package when scaling out.
/// </summary>
public sealed class MemoryCmsCache : ICmsCache
{
    private readonly ConcurrentDictionary<string, Entry> _store = new();

    private readonly record struct Entry(string Json, DateTimeOffset? ExpiresAt);

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt is { } exp && exp <= DateTimeOffset.UtcNow)
                _store.TryRemove(key, out _);
            else
                return Task.FromResult(JsonSerializer.Deserialize<T>(entry.Json));
        }
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class
    {
        var expiresAt = ttl is { } t ? DateTimeOffset.UtcNow.Add(t) : (DateTimeOffset?)null;
        _store[key] = new Entry(JsonSerializer.Serialize(value), expiresAt);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        foreach (var key in _store.Keys)
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
