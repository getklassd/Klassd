using Klassd.Cache.Redis;
using StackExchange.Redis;
using Testcontainers.Redis;
using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>
/// Exercises the real <see cref="RedisCmsCache"/> against a throwaway Redis container
/// (Testcontainers). Requires Docker — skipped automatically when unavailable.
/// </summary>
[SkipWhenDockerUnavailable]
public class RedisCacheIntegrationTests
{
    private static RedisContainer? _container;
    private static IConnectionMultiplexer? _mux;

    [Before(HookType.Class)]
    public static async Task StartContainerAsync()
    {
        if (!DockerProbe.IsAvailable()) return;
        _container = new RedisBuilder("redis:7-alpine").Build();
        await _container.StartAsync();
        _mux = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
    }

    [After(HookType.Class)]
    public static async Task StopContainerAsync()
    {
        if (_mux is not null) await _mux.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }

    private static RedisCmsCache Cache() => new(_mux!);
    private static string Key(string s) => $"itest:{Guid.NewGuid():N}:{s}";

    private sealed record Box(string Value);

    [Test]
    public async Task Set_then_get_round_trips()
    {
        var cache = Cache();
        var key = Key("a");
        await cache.SetAsync(key, new Box("hello"));
        var got = await cache.GetAsync<Box>(key);
        await Assert.That(got).IsNotNull();
        await Assert.That(got!.Value).IsEqualTo("hello");
    }

    [Test]
    public async Task Miss_returns_null()
    {
        await Assert.That(await Cache().GetAsync<Box>(Key("missing"))).IsNull();
    }

    [Test]
    public async Task Ttl_expires_the_entry()
    {
        var cache = Cache();
        var key = Key("ttl");
        await cache.SetAsync(key, new Box("x"), TimeSpan.FromSeconds(1));
        await Task.Delay(1500);
        await Assert.That(await cache.GetAsync<Box>(key)).IsNull();
    }

    [Test]
    public async Task Remove_deletes_the_entry()
    {
        var cache = Cache();
        var key = Key("r");
        await cache.SetAsync(key, new Box("x"));
        await cache.RemoveAsync(key);
        await Assert.That(await cache.GetAsync<Box>(key)).IsNull();
    }

    [Test]
    public async Task RemoveByPrefix_clears_matching_keys_only()
    {
        var cache = Cache();
        var prefix = $"page:{Guid.NewGuid():N}:";
        await cache.SetAsync(prefix + "1", new Box("a"));
        await cache.SetAsync(prefix + "2", new Box("b"));
        var unrelated = Key("keep");
        await cache.SetAsync(unrelated, new Box("c"));

        await cache.RemoveByPrefixAsync(prefix);

        await Assert.That(await cache.GetAsync<Box>(prefix + "1")).IsNull();
        await Assert.That(await cache.GetAsync<Box>(prefix + "2")).IsNull();
        await Assert.That(await cache.GetAsync<Box>(unrelated)).IsNotNull();
    }
}
