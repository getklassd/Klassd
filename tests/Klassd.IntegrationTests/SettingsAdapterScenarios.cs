using Klassd.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>
/// ISettingsStore contract scenarios against a REAL database. Each scenario isolates itself with a
/// unique key so one shared container/db can serve all tests.
/// </summary>
internal static class SettingsAdapterScenarios
{
    private static string NewKey() => "s" + Guid.NewGuid().ToString("N")[..12];

    public static async Task CrudRoundTrip(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
        var key = NewKey();

        // Missing key reads as null.
        await Assert.That(await store.GetAsync(key)).IsNull();

        await store.SetAsync(key, "{\"installId\":\"abc\"}");
        await Assert.That(await store.GetAsync(key)).IsEqualTo("{\"installId\":\"abc\"}");

        // Set replaces (no duplicate row).
        await store.SetAsync(key, "updated");
        await Assert.That(await store.GetAsync(key)).IsEqualTo("updated");

        // Delete; returns false for a missing key.
        await Assert.That(await store.DeleteAsync(key)).IsTrue();
        await Assert.That(await store.GetAsync(key)).IsNull();
        await Assert.That(await store.DeleteAsync(key)).IsFalse();
    }
}
