using Klassd.Abstractions.Caching;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Klassd.Cache.InMemory;
using TUnit.Core;

namespace Klassd.UnitTests;

public class MemoryCmsCacheTests
{
    private static PageRecord Page(string id, string name) =>
        new() { Id = id, ContentId = id, LocaleCode = "en", PageTypeName = "ContentPage", Name = name, Slug = id };

    [Test]
    public async Task Set_then_get_returns_value()
    {
        var cache = new MemoryCmsCache();
        await cache.SetAsync("k", Page("1", "Home"));
        var got = await cache.GetAsync<PageRecord>("k");
        await Assert.That(got).IsNotNull();
        await Assert.That(got!.Name).IsEqualTo("Home");
    }

    [Test]
    public async Task Get_returns_a_copy_not_the_cached_instance()
    {
        var cache = new MemoryCmsCache();
        await cache.SetAsync("k", Page("1", "Home"));

        var first = await cache.GetAsync<PageRecord>("k");
        first!.Name = "Mutated";

        var second = await cache.GetAsync<PageRecord>("k");
        await Assert.That(second!.Name).IsEqualTo("Home"); // mutation of a returned copy must not corrupt the cache
    }

    [Test]
    public async Task Miss_returns_null()
    {
        var cache = new MemoryCmsCache();
        await Assert.That(await cache.GetAsync<PageRecord>("nope")).IsNull();
    }

    [Test]
    public async Task Expired_entry_returns_null()
    {
        var cache = new MemoryCmsCache();
        await cache.SetAsync("k", Page("1", "Home"), TimeSpan.FromMilliseconds(1));
        await Task.Delay(30);
        await Assert.That(await cache.GetAsync<PageRecord>("k")).IsNull();
    }

    [Test]
    public async Task RemoveByPrefix_clears_matching_keys_only()
    {
        var cache = new MemoryCmsCache();
        await cache.SetAsync("page:a", Page("a", "A"));
        await cache.SetAsync("page:b", Page("b", "B"));
        await cache.SetAsync("other", Page("c", "C"));

        await cache.RemoveByPrefixAsync("page:");

        await Assert.That(await cache.GetAsync<PageRecord>("page:a")).IsNull();
        await Assert.That(await cache.GetAsync<PageRecord>("page:b")).IsNull();
        await Assert.That(await cache.GetAsync<PageRecord>("other")).IsNotNull();
    }
}

public class CachingPageStoreTests
{
    private static PageRecord Page(string id) =>
        new() { Id = id, ContentId = id, LocaleCode = "en", PageTypeName = "ContentPage", Name = id, Slug = id };

    [Test]
    public async Task GetById_is_served_from_cache_on_second_call()
    {
        var inner = new CountingPageStore();
        inner.Add(Page("1"));
        var store = new CachingPageStore(inner, new MemoryCmsCache(), new CmsCacheOptions());

        await store.GetByIdAsync("1");
        await store.GetByIdAsync("1");

        await Assert.That(inner.GetByIdCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Write_invalidates_the_cache()
    {
        var inner = new CountingPageStore();
        inner.Add(Page("1"));
        var store = new CachingPageStore(inner, new MemoryCmsCache(), new CmsCacheOptions());

        await store.GetByIdAsync("1");                 // miss -> inner (1)
        await store.GetByIdAsync("1");                 // hit  -> cache
        await store.InsertAsync(Page("2"));            // write -> invalidate
        await store.GetByIdAsync("1");                 // miss -> inner (2)

        await Assert.That(inner.GetByIdCalls).IsEqualTo(2);
    }

    [Test]
    public async Task GetByLocale_is_cached()
    {
        var inner = new CountingPageStore();
        inner.Add(Page("1"));
        inner.Add(Page("2"));
        var store = new CachingPageStore(inner, new MemoryCmsCache(), new CmsCacheOptions());

        var first = await store.GetByLocaleAsync("en");
        var second = await store.GetByLocaleAsync("en");

        await Assert.That(first.Count).IsEqualTo(2);
        await Assert.That(second.Count).IsEqualTo(2);
        await Assert.That(inner.GetByLocaleCalls).IsEqualTo(1);
    }

    /// <summary>Minimal IPageStore that counts read calls, for asserting cache behavior.</summary>
    private sealed class CountingPageStore : IPageStore
    {
        private readonly Dictionary<string, PageRecord> _byId = new();
        public int GetByIdCalls { get; private set; }
        public int GetByLocaleCalls { get; private set; }

        public void Add(PageRecord p) => _byId[p.Id] = p;

        public Task<PageRecord?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            GetByIdCalls++;
            return Task.FromResult(_byId.GetValueOrDefault(id));
        }

        public Task<IReadOnlyList<PageRecord>> GetByLocaleAsync(string localeCode, CancellationToken ct = default)
        {
            GetByLocaleCalls++;
            return Task.FromResult<IReadOnlyList<PageRecord>>(_byId.Values.Where(p => p.LocaleCode == localeCode).ToList());
        }

        public Task<IReadOnlyList<PageRecord>> GetByContentIdAsync(string contentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PageRecord>>(_byId.Values.Where(p => p.ContentId == contentId).ToList());

        public Task<IReadOnlyList<PageRecord>> GetChildrenAsync(string parentId, string localeCode, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PageRecord>>(_byId.Values.Where(p => p.ParentId == parentId && p.LocaleCode == localeCode).ToList());

        public Task<PageRecord?> FindBySlugAsync(string localeCode, string slug, string? excludeId, CancellationToken ct = default) =>
            Task.FromResult<PageRecord?>(null);

        public Task InsertAsync(PageRecord page, CancellationToken ct = default) { _byId[page.Id] = page; return Task.CompletedTask; }
        public Task<PageRecord?> ReplaceAsync(PageRecord page, CancellationToken ct = default) { _byId[page.Id] = page; return Task.FromResult<PageRecord?>(page); }
        public Task UpdateSlugAsync(string id, string slug, DateTime updatedAt, CancellationToken ct = default) { if (_byId.TryGetValue(id, out var p)) p.Slug = slug; return Task.CompletedTask; }
        public Task<bool> DeleteAsync(string id, CancellationToken ct = default) => Task.FromResult(_byId.Remove(id));
    }
}
