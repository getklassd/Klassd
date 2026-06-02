using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Klassd.Backoffice.Modules.Dictionary.Services;
using Klassd.Core.Localization;

namespace Klassd.UnitTests;

public class DictionaryServiceTests
{
    // en (mandatory) ← en-dk falls back to en; da-dk standalone.
    private static LocaleRegistry Registry() => new(
    [
        new LocaleDefinition("en", Mandatory: true),
        new LocaleDefinition("en-dk", FallbackTo: "en"),
        new LocaleDefinition("da-dk"),
    ]);

    private static (DictionaryService Svc, InMemoryDictionaryStore Store) Build()
    {
        var store = new InMemoryDictionaryStore();
        return (new DictionaryService(store, Registry()), store);
    }

    [Test]
    public async Task Resolve_returns_exact_locale_value()
    {
        var (svc, store) = Build();
        await store.UpsertAsync(new DictionaryEntryRecord { Key = "common.no", Values = new() { ["en"] = "No", ["da-dk"] = "Nej" } });

        var en = await svc.ResolveAsync("en");
        await Assert.That(en["common.no"]).IsEqualTo("No");

        var da = await svc.ResolveAsync("da-dk");
        await Assert.That(da["common.no"]).IsEqualTo("Nej");
    }

    [Test]
    public async Task Resolve_falls_back_through_the_chain()
    {
        var (svc, store) = Build();
        // Only "en" provided; en-dk should fall back to en.
        await store.UpsertAsync(new DictionaryEntryRecord { Key = "common.no", Values = new() { ["en"] = "No" } });

        var endk = await svc.ResolveAsync("en-dk");
        await Assert.That(endk["common.no"]).IsEqualTo("No");
    }

    [Test]
    public async Task Resolve_omits_keys_with_no_value_in_the_chain()
    {
        var (svc, store) = Build();
        await store.UpsertAsync(new DictionaryEntryRecord { Key = "only.da", Values = new() { ["da-dk"] = "Kun dansk" } });

        var en = await svc.ResolveAsync("en");          // en has no value, no fallback to da-dk
        await Assert.That(en.ContainsKey("only.da")).IsFalse();

        var da = await svc.ResolveAsync("da-dk");
        await Assert.That(da["only.da"]).IsEqualTo("Kun dansk");
    }

    [Test]
    public async Task Upsert_drops_empty_values_and_requires_a_key()
    {
        var (svc, store) = Build();
        await svc.UpsertAsync("greeting", new Dictionary<string, string> { ["en"] = "Hi", ["da-dk"] = "" });

        var stored = await store.GetAsync("greeting");
        await Assert.That(stored!.Values.ContainsKey("en")).IsTrue();
        await Assert.That(stored.Values.ContainsKey("da-dk")).IsFalse(); // empty dropped

        await Assert.That(async () => await svc.UpsertAsync("  ", new Dictionary<string, string> { ["en"] = "x" }))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Upsert_replaces_then_delete_removes()
    {
        var (svc, store) = Build();
        await svc.UpsertAsync("k", new Dictionary<string, string> { ["en"] = "v1" });
        await svc.UpsertAsync("k", new Dictionary<string, string> { ["en"] = "v2" });
        await Assert.That((await store.GetAsync("k"))!.Values["en"]).IsEqualTo("v2");
        await Assert.That(store.Entries.Count).IsEqualTo(1); // replaced, not duplicated

        await Assert.That(await svc.DeleteAsync("k")).IsTrue();
        await Assert.That(await svc.DeleteAsync("k")).IsFalse(); // already gone
    }
}

/// <summary>In-memory IDictionaryStore for unit tests.</summary>
internal sealed class InMemoryDictionaryStore : IDictionaryStore
{
    public readonly List<DictionaryEntryRecord> Entries = new();

    public Task<IReadOnlyList<DictionaryEntryRecord>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DictionaryEntryRecord>>(Entries.ToList());

    public Task<DictionaryEntryRecord?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(Entries.FirstOrDefault(e => e.Key == key));

    public Task UpsertAsync(DictionaryEntryRecord entry, CancellationToken ct = default)
    {
        Entries.RemoveAll(e => e.Key == entry.Key);
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(Entries.RemoveAll(e => e.Key == key) > 0);
}
