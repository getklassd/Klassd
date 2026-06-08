using System.Security.Claims;
using Klassd.Backoffice.Modules.Auth;
using Klassd.Core.Localization;
using Klassd.Core.PropertyTypes;

namespace Klassd.Backoffice;

/// <summary>
/// Host-facing configuration for the CMS engine, mutated inside the
/// <c>AddKlassd(..., configure)</c> callback.
/// <para>Which page types/properties are localized is declared with the
/// <c>[LocalizedPage]</c> / <c>[Localized]</c> attributes on the types themselves.</para>
/// </summary>
public sealed class CmsOptions
{
    internal LocalizationOptions Localization { get; } = new();
    internal List<IPropertyType> CustomPropertyTypes { get; } = [];

    /// <summary>Read-only view of the custom property types registered via <see cref="AddPropertyType"/>.</summary>
    public IReadOnlyList<IPropertyType> CustomPropertyTypesView => CustomPropertyTypes;

    /// <summary>Run admin seeding (admin/admin) per tenant on startup. Default true.</summary>
    public bool SeedAdminUser { get; set; } = true;

    /// <summary>
    /// How many published versions to keep per page for history/rollback. Older published versions
    /// are pruned beyond this on each publish. 0 = keep all. Default 20. Bound from config
    /// <c>Klassd:Versioning:HistoryLimit</c>.
    /// </summary>
    public int VersionHistoryLimit { get; set; } = 20;

    /// <summary>
    /// Allow headless callers to time-travel block scheduling via <c>?preview=&lt;UTC datetime&gt;</c> on the
    /// <c>/api</c> page GETs (so editors can see future/past content). Default false — bound from config key
    /// <c>Klassd:AllowSchedulePreview</c> so it can be turned OFF in production via appsettings.
    /// </summary>
    public bool AllowSchedulePreview { get; set; }

    // ── Headless delivery auth ────────────────────────────────────────

    /// <summary>
    /// Require an API key on the anonymous content-delivery GETs. Default false (delivery is public +
    /// CORS). Bound from <c>Klassd:Delivery:RequireApiKey</c>. ONLY use this if your frontend
    /// renders server-side (SSR/SSG/BFF) so the key stays off the browser — a key shipped to a browser
    /// SPA is exposed and provides no real protection.
    /// </summary>
    public bool RequireDeliveryApiKey { get; set; }

    /// <summary>The expected delivery API key (config <c>Klassd:Delivery:ApiKey</c>).</summary>
    public string? DeliveryApiKey { get; set; }

    /// <summary>Header carrying the delivery API key (config <c>Klassd:Delivery:ApiKeyHeader</c>). Default <c>X-Api-Key</c>.</summary>
    public string DeliveryApiKeyHeader { get; set; } = "X-Api-Key";

    // ── Anonymous usage telemetry ─────────────────────────────────────

    /// <summary>
    /// Send anonymous usage telemetry (install id, version, which storage/cache/media adapters and
    /// optional features are wired up, aggregate type counts). Default true (opt-out). Bound from
    /// <c>Klassd:Telemetry:Enabled</c>. The payload carries no content/secrets/hostnames. An admin
    /// can flip this from the Settings page (persisted), and <c>KLASSD_TELEMETRY_OPTOUT=1</c> hard-disables it.
    /// </summary>
    public bool TelemetryEnabled { get; set; } = true;

    /// <summary>
    /// Endpoint the telemetry snapshot is POSTed to (config <c>Klassd:Telemetry:Endpoint</c>).
    /// Empty/unset ⇒ collected but not sent (logged at Debug).
    /// </summary>
    public string? TelemetryEndpoint { get; set; }

    // ── External (SSO) login ──────────────────────────────────────────

    /// <summary>
    /// Allow local username/password login. Default true. Set false to force SSO. As a safety
    /// against lockout, this only takes effect when at least one external login is configured —
    /// see <see cref="LocalLoginEnabled"/>.
    /// </summary>
    public bool AllowLocalLogin { get; set; } = true;

    /// <summary>
    /// Whether local login is actually available: <see cref="AllowLocalLogin"/>, OR there is no
    /// external login configured (so disabling local with no SSO provider can't lock everyone out).
    /// </summary>
    public bool LocalLoginEnabled => AllowLocalLogin || ExternalLogins.Count == 0;

    /// <summary>
    /// When an external login succeeds for an unknown identity, create a CMS user for it
    /// automatically. Default true. Set false to require accounts be created up front
    /// (the login is then rejected for unknown identities).
    /// </summary>
    public bool AutoProvisionExternalUsers { get; set; } = true;

    /// <summary>
    /// Override how an external provider's claims map to a CMS user (external id / username / email).
    /// Defaults to <see cref="ExternalUserMapping.Default"/> (sub/NameIdentifier, email, preferred_username).
    /// </summary>
    public Func<ClaimsPrincipal, ExternalUserInfo>? MapExternalUser { get; set; }

    /// <summary>External login providers registered via <c>AddExternalLogin</c> (shown on the login page).</summary>
    internal List<ExternalLoginDescriptor> ExternalLogins { get; } = [];

    /// <summary>Read-only view of the registered external login providers.</summary>
    public IReadOnlyList<ExternalLoginDescriptor> ExternalLoginsView => ExternalLogins;

    // ── Localization passthrough ──────────────────────────────────────

    /// <summary>Add or override a locale. Mirrors <see cref="LocalizationOptions.AddLocale"/>.</summary>
    public CmsOptions AddLocale(string code, Action<LocaleBuilder>? configure = null)
    {
        Localization.AddLocale(code, configure);
        return this;
    }

    // ── Property types ────────────────────────────────────────────────

    /// <summary>Register a custom property (field) type in addition to the Core defaults.</summary>
    public CmsOptions AddPropertyType(IPropertyType propertyType)
    {
        CustomPropertyTypes.Add(propertyType);
        return this;
    }

}
