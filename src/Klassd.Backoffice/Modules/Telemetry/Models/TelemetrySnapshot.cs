namespace Klassd.Backoffice.Modules.Telemetry.Models;

/// <summary>
/// The anonymous usage snapshot sent on startup. Deliberately carries NO content, connection
/// strings, hostnames, file paths, or user data — only a random install id, the engine version,
/// the host runtime, which adapters/features are wired up, and a handful of aggregate counts.
/// </summary>
public sealed record TelemetrySnapshot
{
    /// <summary>Payload schema version, so the collector can evolve the shape safely.</summary>
    public string SchemaVersion { get; init; } = "1";

    /// <summary>Random per-install GUID, persisted locally so restarts don't inflate the install count.</summary>
    public string InstallId { get; init; } = "";

    /// <summary>Klassd engine version (informational/assembly version).</summary>
    public string KlassdVersion { get; init; } = "";

    /// <summary>.NET runtime version, e.g. <c>10.0.0</c>.</summary>
    public string RuntimeVersion { get; init; } = "";

    /// <summary>OS platform + CPU architecture only (e.g. <c>Linux X64</c>) — no build numbers / hostname.</summary>
    public string Os { get; init; } = "";

    /// <summary>Active storage adapter: <c>sqlite</c>, <c>mongodb</c>, <c>postgres</c>, or <c>unknown</c>.</summary>
    public string StorageAdapter { get; init; } = "unknown";

    /// <summary>Active cache adapter: <c>inmemory</c>, <c>redis</c>, <c>hybrid</c>, or <c>none</c>.</summary>
    public string CacheAdapter { get; init; } = "none";

    /// <summary>Distinct media blob backends in use, e.g. <c>["filesystem", "s3"]</c>.</summary>
    public IReadOnlyList<string> MediaBackends { get; init; } = [];

    public bool SearchEnabled { get; init; }
    public bool WebhooksEnabled { get; init; }
    public bool GraphQlEnabled { get; init; }
    public bool GlobalsEnabled { get; init; }

    public int LocaleCount { get; init; }
    public int PageTypeCount { get; init; }
    public int BlockTypeCount { get; init; }
    public int GlobalTypeCount { get; init; }
    public int CustomPropertyTypeCount { get; init; }

    /// <summary>Whether headless delivery is locked behind an API key.</summary>
    public bool RequiresDeliveryApiKey { get; init; }

    /// <summary>How many external (SSO) login providers are configured.</summary>
    public int ExternalLoginProviderCount { get; init; }

    /// <summary>When this snapshot was produced (UTC).</summary>
    public DateTimeOffset SentAt { get; init; }
}
