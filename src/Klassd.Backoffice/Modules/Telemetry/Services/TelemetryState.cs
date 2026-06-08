using System.Text.Json;
using System.Text.Json.Serialization;
using Klassd.Abstractions.Storage;
using Microsoft.Extensions.Logging;

namespace Klassd.Backoffice.Modules.Telemetry.Services;

/// <summary>The small persisted bit of telemetry state.</summary>
public sealed class TelemetryState
{
    /// <summary>Random per-install id. Assigned once on first read.</summary>
    public string InstallId { get; set; } = "";

    /// <summary>
    /// Admin override of the configured default: <c>true</c>/<c>false</c> from the Settings toggle,
    /// or <c>null</c> when the admin hasn't overridden the config/default.
    /// </summary>
    public bool? EnabledOverride { get; set; }

    /// <summary>When the one-time first-run notice was shown (null = not yet).</summary>
    public DateTimeOffset? NoticeShownAtUtc { get; set; }
}

/// <summary>
/// Reads/writes <see cref="TelemetryState"/> as a JSON blob in the durable <see cref="ISettingsStore"/>
/// (the <c>settings</c> table/collection). Storing it in the database — rather than a local file — means
/// the install id and admin opt-out survive across stateless instances (Kubernetes replicas, redeploys),
/// so an install isn't recounted on every restart. All access is best-effort: telemetry must never break
/// or slow the host, so failures fall back to in-memory defaults.
/// </summary>
public sealed class TelemetryStateStore(ISettingsStore settings, ILogger<TelemetryStateStore> logger)
{
    /// <summary>Key the state blob lives under in the settings store.</summary>
    public const string SettingsKey = "telemetry.state";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Loads state, generating + persisting an install id on first use.</summary>
    public async Task<TelemetryState> LoadAsync(CancellationToken ct = default)
    {
        var state = await ReadAsync(ct);
        if (string.IsNullOrWhiteSpace(state.InstallId))
        {
            state.InstallId = Guid.NewGuid().ToString("N");
            await PersistAsync(state, ct);
        }
        return state;
    }

    /// <summary>Applies a mutation to the state and persists it.</summary>
    public async Task<TelemetryState> UpdateAsync(Action<TelemetryState> mutate, CancellationToken ct = default)
    {
        var state = await LoadAsync(ct);
        mutate(state);
        await PersistAsync(state, ct);
        return state;
    }

    private async Task<TelemetryState> ReadAsync(CancellationToken ct)
    {
        try
        {
            var raw = await settings.GetAsync(SettingsKey, ct);
            return string.IsNullOrEmpty(raw)
                ? new()
                : JsonSerializer.Deserialize<TelemetryState>(raw, Json) ?? new();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Klassd telemetry: could not read state; using fresh state.");
            return new();
        }
    }

    private async Task PersistAsync(TelemetryState state, CancellationToken ct)
    {
        try
        {
            await settings.SetAsync(SettingsKey, JsonSerializer.Serialize(state, Json), ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Klassd telemetry: could not persist state (continuing).");
        }
    }
}
