using Klassd.Abstractions.Caching;
using Klassd.Abstractions.Records;
using Klassd.Cache.Hybrid;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;

namespace Klassd.UnitTests;

/// <summary>
/// Unit coverage for the HybridCache adapter, exercised L1-only (no distributed cache
/// registered). Verifies the ICmsCache contract and that the native get-or-create path
/// caches and dedupes.
/// </summary>
public class HybridCmsCacheTests
{
    private static HybridCmsCache NewCache(CmsCacheOptions? options = null)
    {
        var sp = new ServiceCollection().AddHybridCache().Services.BuildServiceProvider();
        return new HybridCmsCache(sp.GetRequiredService<HybridCache>(), options ?? new CmsCacheOptions());
    }

    private static PageRecord Page(string id, string name) =>
        new() { Id = id, ContentId = id, LocaleCode = "en", PageTypeName = "ContentPage", Name = name, Slug = id };

    [Test]
    public async Task Set_then_get_returns_value()
    {
        var cache = NewCache();
        await cache.SetAsync("page:1", Page("1", "Home"));
        var got = await cache.GetAsync<PageRecord>("page:1");
        await Assert.That(got).IsNotNull();
        await Assert.That(got!.Name).IsEqualTo("Home");
    }

    [Test]
    public async Task Miss_returns_null_and_is_not_cached()
    {
        var cache = NewCache();
        await Assert.That(await cache.GetAsync<PageRecord>("page:nope")).IsNull();
        // Probe must not have written a null back: a later set is still observable.
        await cache.SetAsync("page:nope", Page("x", "X"));
        await Assert.That(await cache.GetAsync<PageRecord>("page:nope")).IsNotNull();
    }

    [Test]
    public async Task RemoveByPrefix_clears_the_namespace_via_tags()
    {
        var cache = NewCache();
        await cache.SetAsync("page:a", Page("a", "A"));
        await cache.SetAsync("page:b", Page("b", "B"));
        await cache.SetAsync("media:c", Page("c", "C"));

        await cache.RemoveByPrefixAsync("page:");

        await Assert.That(await cache.GetAsync<PageRecord>("page:a")).IsNull();
        await Assert.That(await cache.GetAsync<PageRecord>("page:b")).IsNull();
        await Assert.That(await cache.GetAsync<PageRecord>("media:c")).IsNotNull(); // other namespace untouched
    }

    [Test]
    public async Task GetOrCreate_invokes_factory_once_then_serves_from_cache()
    {
        var cache = NewCache();
        var calls = 0;
        Task<PageRecord?> Factory(CancellationToken _) { calls++; return Task.FromResult<PageRecord?>(Page("1", "Home")); }

        var first = await cache.GetOrCreateAsync("page:1", Factory);
        var second = await cache.GetOrCreateAsync("page:1", Factory);

        await Assert.That(first!.Name).IsEqualTo("Home");
        await Assert.That(second!.Name).IsEqualTo("Home");
        await Assert.That(calls).IsEqualTo(1);
    }
}
