using System.Text.Json;
using Klassd.Abstractions.Caching;
using StackExchange.Redis;

namespace Klassd.Cache.Redis;

/// <summary>
/// Distributed <see cref="ICmsCache"/> backed by Redis (StackExchange.Redis). Values are
/// JSON. <see cref="RemoveByPrefixAsync"/> scans keys via <c>SCAN</c> on each primary
/// endpoint — invalidation runs on writes (rare) while reads stay O(1).
/// </summary>
public sealed class RedisCmsCache(IConnectionMultiplexer multiplexer) : ICmsCache
{
    private readonly IDatabase _db = multiplexer.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var value = await _db.StringGetAsync(key);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(value.ToString());
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class =>
        _db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl, When.Always);

    public Task RemoveAsync(string key, CancellationToken ct = default) =>
        _db.KeyDeleteAsync(key);

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        foreach (var endpoint in multiplexer.GetEndPoints())
        {
            var server = multiplexer.GetServer(endpoint);
            if (!server.IsConnected || server.IsReplica) continue;

            await foreach (var key in server.KeysAsync(pattern: prefix + "*").WithCancellation(ct))
                await _db.KeyDeleteAsync(key);
        }
    }
}
