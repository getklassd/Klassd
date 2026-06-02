using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>
/// IDictionaryStore contract scenarios against a REAL database. Each scenario isolates itself with a
/// unique key prefix so one shared container/db can serve all tests.
/// </summary>
internal static class DictionaryAdapterScenarios
{
    private static string NewKey() => "k" + Guid.NewGuid().ToString("N")[..12];

    public static async Task CrudRoundTrip(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDictionaryStore>();
        var key = NewKey();

        // Insert with a per-locale values map.
        await store.UpsertAsync(new DictionaryEntryRecord
        {
            Key = key,
            Values = new() { ["en"] = "No", ["da-dk"] = "Nej", ["de"] = "Nein" },
        });

        var fetched = await store.GetAsync(key);
        await Assert.That(fetched).IsNotNull();
        await Assert.That(fetched!.Values["en"]).IsEqualTo("No");
        await Assert.That(fetched.Values["da-dk"]).IsEqualTo("Nej");
        await Assert.That(fetched.Values["de"]).IsEqualTo("Nein");

        await Assert.That((await store.GetAllAsync()).Any(e => e.Key == key)).IsTrue();

        // Upsert replaces the values (not duplicates).
        await store.UpsertAsync(new DictionaryEntryRecord { Key = key, Values = new() { ["en"] = "Nope" } });
        var updated = await store.GetAsync(key);
        await Assert.That(updated!.Values["en"]).IsEqualTo("Nope");
        await Assert.That(updated.Values.ContainsKey("da-dk")).IsFalse(); // fully replaced

        // Delete; returns false for a missing key.
        await Assert.That(await store.DeleteAsync(key)).IsTrue();
        await Assert.That(await store.GetAsync(key)).IsNull();
        await Assert.That(await store.DeleteAsync(key)).IsFalse();
    }
}
