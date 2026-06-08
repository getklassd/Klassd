using System.Text.Json;
using Klassd.Abstractions;
using Klassd.Backoffice.Modules.Auth;
using Klassd.Backoffice.Modules.Pages;
using Klassd.Backoffice.Modules.Preferences;
using Klassd.Backoffice.Modules.Users;
using Klassd.Backoffice.State;
using Klassd.Core.Localization;
using Klassd.Core.PropertyTypes;
using Klassd.Core.PropertyTypes.Defaults;
using Klassd.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Klassd.Backoffice;

/// <summary>
/// Install surface for the Klassd engine (Blazor Interactive Server admin at
/// <c>/admin</c> + headless JSON API at <c>/api</c>).
/// <para>Host <c>Program.cs</c> — two lines:</para>
/// <code>
/// builder.Services.AddKlassd(builder.Configuration)
///                 .UseSqlite(builder.Configuration.GetSection("Sqlite"));
/// var app = builder.Build();
/// app.UseKlassd();   // everything: auth + antiforgery + seed/init + static assets + /api + Blazor admin
/// app.Run();
/// </code>
/// <para>Advanced hosts that need to control middleware ordering can instead add their
/// own auth/antiforgery middleware and call <see cref="MapKlassd"/> for the
/// endpoints (it also runs storage init + admin seeding).</para>
/// </summary>
public static class KlassdExtensions
{
    internal static readonly IReadOnlyList<IModule> Modules =
        [new AuthModule(), new PreferencesModule(), new PageModule(), new UsersModule(),
         new Modules.Media.MediaModule(), new Modules.Dictionary.DictionaryModule(),
         new Modules.Globals.GlobalModule(), new Modules.Telemetry.TelemetryModule()];

    /// <summary>
    /// CORS policy applied to the anonymous headless content-delivery GETs. Origins come from config
    /// <c>Klassd:Cors:AllowedOrigins</c> (array); empty/unset ⇒ any origin. NOTE: CORS only limits
    /// which browser origins may fetch via JS — it is not an auth boundary for server-side callers, so
    /// delivery content is genuinely public.
    /// </summary>
    public const string DeliveryCorsPolicy = "KlassdDelivery";

    /// <summary>
    /// Marks a route as public content delivery: anonymous + the delivery CORS policy + the optional
    /// API-key gate (no-op unless <c>Klassd:Delivery:RequireApiKey</c> is set). Used by the
    /// page/dictionary/media delivery GETs.
    /// </summary>
    public static RouteHandlerBuilder AsPublicDelivery(this RouteHandlerBuilder builder) =>
        builder.AllowAnonymous().RequireCors(DeliveryCorsPolicy).AddEndpointFilter<DeliveryApiKeyFilter>();

    public static ICmsBuilder AddKlassd(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<CmsOptions>? configure = null)
    {
        // Bind the whole Klassd section once (typed), then seed CmsOptions defaults from it —
        // an explicit configure callback can still override.
        var settings = configuration.GetSection(KlassdSettings.SectionName).Get<KlassdSettings>() ?? new();
        var options = new CmsOptions
        {
            AllowSchedulePreview = settings.AllowSchedulePreview,
            VersionHistoryLimit = settings.Versioning.HistoryLimit,
            RequireDeliveryApiKey = settings.Delivery.RequireApiKey,
            DeliveryApiKey = settings.Delivery.ApiKey,
            DeliveryApiKeyHeader = settings.Delivery.ApiKeyHeader,
            TelemetryEnabled = settings.Telemetry.Enabled,
            TelemetryEndpoint = settings.Telemetry.Endpoint,
        };
        configure?.Invoke(options);

        // ── Headless API JSON (camelCase) ─────────────────────────────
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            // Source-generated (de)serialization for the known delivery/admin types (faster, no
            // per-request reflection); the default reflection resolver remains in the chain as a
            // fallback for anything not registered in KlassdJsonContext.
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, KlassdJsonContext.Default);
        });

        // ── Localization ──────────────────────────────────────────────
        var localization = options.Localization;
        localization.LoadFrom(configuration.GetSection("CmsLocalization:Locales").Get<List<LocaleConfig>>());
        services.AddSingleton(localization);
        services.AddSingleton(new LocaleRegistry(localization.Locales));

        // ── Content type registries ───────────────────────────────────
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var propertyTypes = new PropertyTypeRegistry(
            DefaultPropertyTypes.All
                .Concat(options.CustomPropertyTypes)
                .Concat(PropertyEditorDiscovery.Discover(assemblies))); // auto-discovered [PropertyEditor] components
        services.AddSingleton(propertyTypes);
        var pageRegistry = new PageTypeRegistry(assemblies, propertyTypes);
        var globalRegistry = new GlobalTypeRegistry(assemblies, propertyTypes);
        services.AddSingleton(pageRegistry);
        services.AddSingleton(new BlockTypeRegistry(assemblies, propertyTypes));
        services.AddSingleton(globalRegistry);

        // ── Index + search plan from [Indexable] (consumed by storage adapters + admin search) ──
        // Distinct camelCase data keys per kind. Blocks are searchable but NOT DB-indexed (their
        // values live nested in block-area JSON arrays — no simple JSON-path expression index).
        var pageKeys = pageRegistry.GetAll().SelectMany(t => t.Fields)
            .Where(f => f.Indexable).Select(f => f.Name).Distinct().ToArray();
        var globalKeys = globalRegistry.GetAll().SelectMany(t => t.Fields)
            .Where(f => f.Indexable).Select(f => f.Name).Distinct().ToArray();

        services.AddSingleton(new Abstractions.Storage.IndexDefinitions
        {
            JsonIndexes =
            [
                .. pageKeys.Select(k => new Abstractions.Storage.JsonFieldIndex("pages", "data", k)),
                .. globalKeys.Select(k => new Abstractions.Storage.JsonFieldIndex("globals", "data", k)),
            ],
            // Media has no code-first class — index its fixed search columns directly.
            ColumnIndexes =
            [
                new Abstractions.Storage.ColumnIndex("media", "file_name", "FileName"),
                new Abstractions.Storage.ColumnIndex("media", "display_name", "DisplayName"),
                new Abstractions.Storage.ColumnIndex("media", "alt_text", "AltText"),
            ],
        });
        services.AddSingleton(new Abstractions.Storage.SearchableFields
        {
            PageFields = pageKeys,
            GlobalFields = globalKeys,
        });

        // ── Engine services (scoped) ──────────────────────────────────
        foreach (var module in Modules)
            module.RegisterServices(services, configuration);

        // ── Cookie authentication ─────────────────────────────────────
        // Primary cookie (cms_auth) + a temporary external cookie (cms_external) that SSO
        // handlers sign into; the external-login callback exchanges it for the primary cookie.
        services.AddAuthentication(CmsAuthSchemes.Cookie)
            .AddCookie(CmsAuthSchemes.Cookie, o =>
            {
                o.Cookie.Name = "cms_auth";
                o.Cookie.HttpOnly = true;
                o.LoginPath = "/admin/login";
                o.LogoutPath = "/admin/logout";
                o.AccessDeniedPath = "/admin/login";
                o.ExpireTimeSpan = TimeSpan.FromHours(168);
                o.SlidingExpiration = true;
            })
            .AddCookie(CmsAuthSchemes.External, o =>
            {
                o.Cookie.Name = "cms_external";
                o.Cookie.HttpOnly = true;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            });
        services.AddAuthorization();
        services.AddCascadingAuthenticationState();
        services.AddHttpContextAccessor();

        // CORS for the public delivery GETs. Restrict to configured origins, or any origin if unset.
        var corsOrigins = settings.Cors.AllowedOrigins;
        services.AddCors(cors => cors.AddPolicy(DeliveryCorsPolicy, policy =>
        {
            if (corsOrigins.Length > 0) policy.WithOrigins(corsOrigins);
            else policy.AllowAnyOrigin();
            policy.WithMethods("GET").AllowAnyHeader();
        }));

        // ── Blazor admin (Interactive Server) ─────────────────────────
        services.AddRazorComponents().AddInteractiveServerComponents();

        // ── Media (empty by default; AddMedia overrides the registry + adds blob stores) ─
        services.AddSingleton(new Abstractions.Media.MediaSectionRegistry([]));
        services.AddScoped<Abstractions.Media.MediaService>();

        // ── Domain events ─────────────────────────────────────────────
        // Fan-out publisher over any registered ICmsEventListener (webhooks, search indexer, …).
        // No listeners ⇒ no-op. Listener packages add themselves via AddSingleton<ICmsEventListener,…>.
        services.TryAddSingleton<Abstractions.Events.ICmsEventPublisher, Abstractions.Events.CmsEventPublisher>();

        // ── In-process notifications (synchronous, ordered, cancelable; for business hooks) ─
        services.TryAddScoped<Abstractions.Notifications.ICmsNotifier, CmsNotifier>();

        // ── Scoped UI state (mirrors the former Vue composables) ───────
        services.AddScoped<AdminUser>();
        services.AddScoped<ToastService>();
        services.AddScoped<ContentTypeCatalog>();
        services.AddScoped<LocaleState>();
        services.AddScoped<PageTreeState>();
        services.AddScoped<EditorPanelState>();
        services.AddScoped<State.GlobalEditorState>();
        services.AddScoped<Modules.Search.SearchService>();

        // Captured so UseKlassd can honor SeedAdminUser at startup.
        services.AddSingleton(options);

        return new CmsBuilder(services, configuration);
    }

    /// <summary>
    /// One-call setup: adds the engine middleware (authentication, authorization,
    /// antiforgery) and then maps everything via <see cref="MapKlassd"/>
    /// (RCL static assets, the headless <c>/api</c> + auth endpoints, the Blazor admin,
    /// plus one-time storage init + admin seeding). This is all a typical host needs.
    /// </summary>
    public static WebApplication UseKlassd(this WebApplication app)
    {
        app.UseCors();            // honors per-endpoint RequireCors metadata on the delivery GETs
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapKlassd();
        return app;
    }

    /// <summary>
    /// Maps RCL static assets, the headless <c>/api</c> + auth endpoints, and the Blazor
    /// admin; also runs storage initialization (schema/indexes) and admin seeding once.
    /// Use this directly only when composing the middleware pipeline yourself; otherwise
    /// call <see cref="UseKlassd"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapKlassd(this IEndpointRouteBuilder endpoints)
    {
        // Initialize storage (schema/indexes) THEN seed — both once, before serving traffic.
        using (var scope = endpoints.ServiceProvider.CreateScope())
        {
            var sp = scope.ServiceProvider;

            // Fail fast with a clear message if no storage adapter was registered.
            if (sp.GetService<Abstractions.Storage.IPageStore>() is null)
                throw new InvalidOperationException(
                    "Klassd: no storage adapter is registered. Call a storage adapter " +
                    "(e.g. .UseSqlite(...), .UseMongoDb(...) or .UsePostgres(...)) on the builder returned by AddKlassd().");

            foreach (var initializer in sp.GetServices<Abstractions.Storage.IStorageInitializer>())
                initializer.InitializeAsync().GetAwaiter().GetResult();

            if (sp.GetRequiredService<CmsOptions>().SeedAdminUser)
                sp.GetRequiredService<Modules.Auth.Services.UserService>()
                    .SeedAdminAsync().GetAwaiter().GetResult();

            // Fail loud (don't silently serve UTC) if a market time zone can't be resolved — usually
            // missing OS tz data in a slim container (Alpine: `apk add --no-cache tzdata`).
            var unresolvedZones = sp.GetRequiredService<Core.Localization.LocaleRegistry>().UnresolvedTimeZones();
            if (unresolvedZones.Count > 0)
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Klassd.Scheduling")
                    .LogWarning(
                        "Klassd: {Count} locale time zone(s) could not be resolved on this host and will " +
                        "fall back to UTC, so market-local content scheduling will be off. Install OS tz data " +
                        "(Debian/Ubuntu images include it; Alpine: 'apk add --no-cache tzdata'). Unresolved: {Zones}.",
                        unresolvedZones.Count,
                        string.Join(", ", unresolvedZones.Select(z => $"{z.Code}→{z.TimeZone}")));

            // Fail loud if delivery is locked behind an API key that wasn't configured (all delivery → 503).
            var cmsOptions = sp.GetRequiredService<CmsOptions>();
            if (cmsOptions.RequireDeliveryApiKey && string.IsNullOrEmpty(cmsOptions.DeliveryApiKey))
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Klassd.Delivery")
                    .LogWarning(
                        "Klassd: delivery API key is required (Klassd:Delivery:RequireApiKey=true) but " +
                        "none is configured (Klassd:Delivery:ApiKey) — every content-delivery request will be rejected with 503.");
        }

        endpoints.MapStaticAssets();

        foreach (var module in Modules)
            module.MapEndpoints(endpoints);

        endpoints.MapRazorComponents<Components.App>()
            .AddInteractiveServerRenderMode();

        return endpoints;
    }
}
