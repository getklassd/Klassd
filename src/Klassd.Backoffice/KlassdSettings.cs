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
}
