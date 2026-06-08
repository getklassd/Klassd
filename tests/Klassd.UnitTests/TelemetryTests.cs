using System.Collections.Concurrent;
using System.Reflection;
using Klassd.Abstractions.Media;
using Klassd.Abstractions.Storage;
using Klassd.Backoffice;
using Klassd.Backoffice.Modules.Telemetry.Services;
using Klassd.Core.Localization;
using Klassd.Core.PropertyTypes;
using Klassd.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Klassd.UnitTests;

public class TelemetryStateStoreTests
{
    [Test]
    public async Task Load_generates_and_persists_a_stable_install_id()
    {
        var settings = new InMemorySettingsStore();
        var store = new TelemetryStateStore(settings, NullLogger<TelemetryStateStore>.Instance);

        var id = (await store.LoadAsync()).InstallId;
        await Assert.That(id).IsNotEmpty();

        // A fresh store over the SAME settings backend must read the same persisted id.
        var reopened = new TelemetryStateStore(settings, NullLogger<TelemetryStateStore>.Instance);
        await Assert.That((await reopened.LoadAsync()).InstallId).IsEqualTo(id);
    }

    [Test]
    public async Task Update_persists_the_admin_override_to_the_settings_store()
    {
        var settings = new InMemorySettingsStore();
        var store = new TelemetryStateStore(settings, NullLogger<TelemetryStateStore>.Instance);

        await store.UpdateAsync(s => s.EnabledOverride = false);

        // Survives via the shared backend (mirrors a stateless instance reading the DB).
        var reopened = new TelemetryStateStore(settings, NullLogger<TelemetryStateStore>.Instance);
        await Assert.That((await reopened.LoadAsync()).EnabledOverride == false).IsTrue();
        await Assert.That(await settings.GetAsync(TelemetryStateStore.SettingsKey)).IsNotNull();
    }
}

public class TelemetryServiceResolutionTests
{
    private static TelemetryService Build(CmsOptions options)
    {
        var store = new TelemetryStateStore(new InMemorySettingsStore(), NullLogger<TelemetryStateStore>.Instance);
        // ResolveAsync only needs options + state store, so an empty provider is fine here.
        var sp = new ServiceCollection().BuildServiceProvider();
        return new TelemetryService(sp, options, store);
    }

    [Test]
    public async Task Defaults_to_the_configured_value()
    {
        var on = Build(new CmsOptions { TelemetryEnabled = true });
        var off = Build(new CmsOptions { TelemetryEnabled = false });

        var (onEnabled, onSource) = await on.ResolveAsync();
        await Assert.That(onEnabled).IsTrue();
        await Assert.That(onSource).IsEqualTo(TelemetrySource.Configuration);
        await Assert.That(await off.IsEnabledAsync()).IsFalse();
    }

    [Test]
    public async Task Admin_override_beats_configuration()
    {
        var svc = Build(new CmsOptions { TelemetryEnabled = true });

        await svc.SetEnabledAsync(false);
        var (enabled, source) = await svc.ResolveAsync();
        await Assert.That(enabled).IsFalse();
        await Assert.That(source).IsEqualTo(TelemetrySource.AdminSetting);

        await svc.ClearOverrideAsync();
        await Assert.That(await svc.IsEnabledAsync()).IsTrue();
    }
}

public class TelemetrySnapshotTests
{
    [Test]
    public async Task Snapshot_reports_version_counts_and_no_adapters_when_bare()
    {
        var options = new CmsOptions { TelemetryEnabled = true, RequireDeliveryApiKey = true };
        var props = new PropertyTypeRegistry([]);
        Assembly[] none = [];

        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton(props);
        services.AddSingleton(new PageTypeRegistry(none, props));
        services.AddSingleton(new BlockTypeRegistry(none, props));
        services.AddSingleton(new GlobalTypeRegistry(none, props));
        services.AddSingleton(new LocaleRegistry([new LocaleDefinition("en", Mandatory: true)]));
        services.AddSingleton(new MediaSectionRegistry([]));
        var sp = services.BuildServiceProvider();

        var store = new TelemetryStateStore(new InMemorySettingsStore(), NullLogger<TelemetryStateStore>.Instance);
        var snapshot = await new TelemetryService(sp, options, store).BuildSnapshotAsync();

        await Assert.That(snapshot.InstallId).IsNotEmpty();
        await Assert.That(snapshot.KlassdVersion).IsNotEmpty();
        await Assert.That(snapshot.StorageAdapter).IsEqualTo("unknown"); // no adapter registered
        await Assert.That(snapshot.CacheAdapter).IsEqualTo("none");
        await Assert.That(snapshot.MediaBackends).IsEmpty();
        await Assert.That(snapshot.LocaleCount).IsEqualTo(1);
        await Assert.That(snapshot.RequiresDeliveryApiKey).IsTrue();
    }
}

public class HttpTelemetrySinkTests
{
    [Test]
    public async Task Does_not_call_http_when_no_endpoint_is_configured()
    {
        // The throwing factory proves the sink short-circuits before touching HTTP.
        var sink = new HttpTelemetrySink(new ThrowingHttpClientFactory(),
            new CmsOptions { TelemetryEndpoint = null }, NullLogger<HttpTelemetrySink>.Instance);

        await sink.SendAsync(new() { InstallId = "x" });
        // No exception ⇒ the HTTP path was never taken.
    }
}

/// <summary>In-memory <see cref="ISettingsStore"/> standing in for a DB adapter in unit tests.</summary>
internal sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly ConcurrentDictionary<string, string> _store = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(key, out var v) ? v : null);

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(_store.TryRemove(key, out _));
}

internal sealed class ThrowingHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => throw new InvalidOperationException("HTTP should not be called.");
}
