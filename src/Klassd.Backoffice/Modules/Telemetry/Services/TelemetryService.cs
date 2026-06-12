using System.Reflection;
using System.Runtime.InteropServices;
using Klassd.Abstractions.Events;
using Klassd.Abstractions.Media;
using Klassd.Abstractions.Search;
using Klassd.Backoffice.Modules.Telemetry.Models;
using Klassd.Core.Localization;
using Klassd.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Backoffice.Modules.Telemetry.Services;

/// <summary>How the current enabled/disabled decision was reached (shown in the admin Settings page).</summary>
public enum TelemetrySource { Configuration, AdminSetting, EnvironmentOptOut }

/// <summary>
/// Builds the anonymous <see cref="TelemetrySnapshot"/> from the live DI container and resolves
/// whether telemetry is currently enabled. Detection is structural (which adapters/listeners are
/// registered) so it stays correct as hosts wire up different packages.
/// </summary>
public sealed class TelemetryService(
    IServiceProvider services,
    CmsOptions options,
    TelemetryStateStore stateStore)
{
    /// <summary>Environment variable that hard-disables telemetry regardless of config/admin setting.</summary>
    public const string OptOutEnvVar = "KLASSD_TELEMETRY_OPTOUT";

    public async Task<string> GetInstallIdAsync(CancellationToken ct = default) =>
        (await stateStore.LoadAsync(ct)).InstallId;

    /// <summary>True when <see cref="OptOutEnvVar"/> is set to a truthy value (1/true/yes).</summary>
    public bool IsForcedOffByEnv
    {
        get
        {
            var v = Environment.GetEnvironmentVariable(OptOutEnvVar)?.Trim();
            return v is "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Resolved enabled state: env opt-out wins, then the admin override, then config/default.</summary>
    public async Task<(bool Enabled, TelemetrySource Source)> ResolveAsync(CancellationToken ct = default)
    {
        if (IsForcedOffByEnv) return (false, TelemetrySource.EnvironmentOptOut);
        var over = (await stateStore.LoadAsync(ct)).EnabledOverride;
        if (over is { } b) return (b, TelemetrySource.AdminSetting);
        return (options.TelemetryEnabled, TelemetrySource.Configuration);
    }

    public async Task<bool> IsEnabledAsync(CancellationToken ct = default) => (await ResolveAsync(ct)).Enabled;

    /// <summary>Admin toggle: persist an explicit on/off that overrides config. No-op under env opt-out.</summary>
    public Task SetEnabledAsync(bool enabled, CancellationToken ct = default) =>
        stateStore.UpdateAsync(s => s.EnabledOverride = enabled, ct);

    /// <summary>Clears the admin override so the configured default applies again.</summary>
    public Task ClearOverrideAsync(CancellationToken ct = default) =>
        stateStore.UpdateAsync(s => s.EnabledOverride = null, ct);

    /// <summary>Builds the snapshot that would be (or was) sent. Pure read — safe to call from the admin UI.</summary>
    public async Task<TelemetrySnapshot> BuildSnapshotAsync(CancellationToken ct = default)
    {
        var pages = services.GetRequiredService<PageTypeRegistry>();
        var blocks = services.GetRequiredService<BlockTypeRegistry>();
        var globals = services.GetRequiredService<GlobalTypeRegistry>();
        var locales = services.GetRequiredService<LocaleRegistry>();

        return new TelemetrySnapshot
        {
            InstallId = await GetInstallIdAsync(ct),
            KlassdVersion = Version(),
            RuntimeVersion = Environment.Version.ToString(),
            Os = $"{OsPlatform()} {RuntimeInformation.OSArchitecture}",
            StorageAdapter = DetectStorageAdapter(),
            CacheAdapter = DetectCacheAdapter(),
            MediaBackends = DetectMediaBackends(),
            SearchEnabled = services.GetService<ICmsSearchIndex>() is not null,
            WebhooksEnabled = HasListenerFromAssembly("Klassd.Webhooks"),
            GraphQlEnabled = IsAssemblyLoaded("Klassd.GraphQL"),
            GlobalsEnabled = globals.GetAll().Count > 0,
            LocaleCount = locales.All.Count,
            PageTypeCount = pages.GetAll().Count,
            BlockTypeCount = blocks.GetAll().Count,
            GlobalTypeCount = globals.GetAll().Count,
            CustomPropertyTypeCount = options.CustomPropertyTypesView.Count,
            RequiresDeliveryApiKey = options.RequireDeliveryApiKey,
            // External login providers are registered with Klassd.Auth's ExternalLoginRegistry.
            ExternalLoginProviderCount =
                services.GetService<Klassd.Auth.AspNetCore.Cookies.ExternalLoginRegistry>()?.Providers.Count ?? 0,
            SentAt = DateTimeOffset.UtcNow,
        };
    }

    // ── Structural detection ──────────────────────────────────────────

    private string DetectStorageAdapter()
    {
        // Each adapter registers its own IStorageInitializer from its own assembly — a clean,
        // un-decorated signal (the IPageStore may be wrapped by the caching decorator).
        foreach (var init in services.GetServices<Abstractions.Storage.IStorageInitializer>())
        {
            var asm = init.GetType().Assembly.GetName().Name ?? "";
            if (asm.EndsWith("Sqlite", StringComparison.OrdinalIgnoreCase)) return "sqlite";
            if (asm.Contains("Mongo", StringComparison.OrdinalIgnoreCase)) return "mongodb";
            if (asm.Contains("Postgres", StringComparison.OrdinalIgnoreCase)) return "postgres";
        }
        return "unknown";
    }

    private string DetectCacheAdapter()
    {
        var cache = services.GetService<Abstractions.Caching.ICmsCache>();
        var asm = cache?.GetType().Assembly.GetName().Name ?? "";
        if (asm.Contains("InMemory", StringComparison.OrdinalIgnoreCase)) return "inmemory";
        if (asm.Contains("Redis", StringComparison.OrdinalIgnoreCase)) return "redis";
        if (asm.Contains("Hybrid", StringComparison.OrdinalIgnoreCase)) return "hybrid";
        return "none";
    }

    private IReadOnlyList<string> DetectMediaBackends()
    {
        var backends = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in services.GetRequiredService<MediaSectionRegistry>().Sections)
        {
            var store = services.GetKeyedService<IBlobStore>(section.Name);
            var name = store?.GetType().Name ?? "";
            if (name.StartsWith("FileSystem", StringComparison.OrdinalIgnoreCase)) backends.Add("filesystem");
            else if (name.StartsWith("S3", StringComparison.OrdinalIgnoreCase)) backends.Add("s3");
            else if (name.StartsWith("Gcs", StringComparison.OrdinalIgnoreCase)) backends.Add("gcs");
        }
        return backends.OrderBy(b => b).ToArray();
    }

    private bool HasListenerFromAssembly(string assemblyName) =>
        services.GetServices<ICmsEventListener>()
            .Any(l => string.Equals(l.GetType().Assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));

    private static bool IsAssemblyLoaded(string assemblyName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));

    private static string OsPlatform() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux"
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS"
        : "Other";

    private static string Version() =>
        typeof(TelemetryService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            is { Length: > 0 } infoFull
            // strip the "+<gitsha>" build-metadata suffix the SDK appends
            ? infoFull.Split('+')[0]
            : typeof(TelemetryService).Assembly.GetName().Version?.ToString() ?? "unknown";
}
