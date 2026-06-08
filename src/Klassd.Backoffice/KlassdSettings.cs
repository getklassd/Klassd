namespace Klassd.Backoffice;

/// <summary>
/// Strongly-typed view of the <c>Klassd</c> configuration section (appsettings.json / env vars).
/// Bound once in <c>AddKlassd</c>; the values seed <see cref="CmsOptions"/> defaults (an explicit
/// configure callback can still override them).
/// <code>
/// "Klassd": {
///   "AllowSchedulePreview": false,
///   "Cors":     { "AllowedOrigins": [ "https://www.example.com" ] },
///   "Delivery": { "RequireApiKey": false, "ApiKey": "", "ApiKeyHeader": "X-Api-Key" }
/// }
/// </code>
/// </summary>
public sealed class KlassdSettings
{
    public const string SectionName = "Klassd";

    /// <summary>Allow <c>?preview=&lt;UTC datetime&gt;</c> time-travel on the page delivery GETs.</summary>
    public bool AllowSchedulePreview { get; set; }

    public CorsSettings Cors { get; set; } = new();
    public DeliverySettings Delivery { get; set; } = new();
    public VersioningSettings Versioning { get; set; } = new();
    public TelemetrySettings Telemetry { get; set; } = new();

    public sealed class CorsSettings
    {
        /// <summary>Browser origins allowed to fetch the delivery GETs. Empty ⇒ any origin.</summary>
        public string[] AllowedOrigins { get; set; } = [];
    }

    public sealed class DeliverySettings
    {
        /// <summary>Require an API key on the delivery GETs (server-side frontends only). Default false (public).</summary>
        public bool RequireApiKey { get; set; }
        public string? ApiKey { get; set; }
        public string ApiKeyHeader { get; set; } = "X-Api-Key";
    }

    public sealed class VersioningSettings
    {
        /// <summary>Published versions kept per page for history/rollback (0 = keep all). Default 20.</summary>
        public int HistoryLimit { get; set; } = 20;
    }

    /// <summary>
    /// Anonymous usage telemetry. Enabled by default (opt-out) so the project can see how many
    /// installs exist and which adapters/features are used — the payload carries NO content, no
    /// connection strings, no hostnames, just a random install id, the version, and aggregate
    /// counts/flags. Disable with <c>Klassd:Telemetry:Enabled=false</c>, the admin Settings toggle,
    /// or the <c>KLASSD_TELEMETRY_OPTOUT=1</c> environment variable (which hard-overrides everything).
    /// </summary>
    public sealed class TelemetrySettings
    {
        /// <summary>The Klassd project's hosted collector. Override to self-host; set empty to disable sending.</summary>
        public const string DefaultEndpoint = "https://telemetry.getklassd.com";

        /// <summary>Send anonymous usage telemetry. Default true (opt-out).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Ingest endpoint the snapshot is POSTed to. Defaults to <see cref="DefaultEndpoint"/>; set to
        /// empty to collect-but-not-send (logged at Debug), or to your own URL to self-host the collector.
        /// </summary>
        public string? Endpoint { get; set; } = DefaultEndpoint;
    }
}
