using Klassd.Abstractions.Media;
using Klassd.Backoffice;
using Klassd.Cache.InMemory;
using Klassd.Cache.Redis;
using Klassd.Data.MongoDb;
using Klassd.Data.Postgres;
using Klassd.Data.Sqlite;
using Klassd.Media.FileSystem;
using Klassd.Media.GoogleCloud;
using Klassd.Media.S3;

var builder = WebApplication.CreateBuilder(args);

// ── Install the CMS engine ────────────────────────────────────────────
// Page types, block types and property types are discovered from this app's
// assembly (see Content/). The host configures cross-cutting concerns only.
builder.Services
    // Page/block types AND [PropertyEditor]-marked editor components (e.g.
    // Content/ColorEditor.razor) are auto-discovered from this app's assembly.
    .AddKlassd(builder.Configuration)
    // Choose a storage adapter (Mongo / Postgres / SQLite).
//  .UseMongoDb(builder.Configuration.GetSection("MongoDB"));
//  .UsePostgres(builder.Configuration.GetSection("Postgres"));
    .UseSqlite(builder.Configuration.GetSection("Sqlite"))
    // Optional read-through page cache. Pick a cache adapter (or omit for no caching).
    .UseInMemoryCache()
//  .UseRedisCache(builder.Configuration.GetSection("Redis"))
    // Media: multiple named sections, each on its own blob adapter. (FileSystem here so the
    // sample runs with no cloud creds; swap per section to .UseS3(...) / .UseGoogleCloudStorage(...).)
    .AddMedia(media =>
    {
        media.AddSection("images", s => s
            .UseFileSystem(Path.Combine(builder.Environment.ContentRootPath, "media", "images"))
            .AllowContentTypes("image/*")
            .ResizeImages(2000)
            .Breakpoints("default", "mobile", "tablet", "desktop"));   // focal-point breakpoints (admin dropdown)
        media.AddSection("documents", s => s
            .UseFileSystem(Path.Combine(builder.Environment.ContentRootPath, "media", "documents"))
            .AllowContentTypes("application/pdf"));
    });

// ── Optional: single sign-on (SSO) for the backoffice ─────────────────
// Authentication is delegated to the external Klassd.Auth suite, wired internally by AddKlassd.
// To add an SSO provider, resolve the stashed IAuthBuilder and call one of the Klassd.Auth
// extensions (Klassd.Auth.OpenIdConnect package: AddOpenIdConnect / AddEntraId / AddGoogle).
// Each provider adds a "Sign in with …" button to /admin/login; on first sign-in the user is
// provisioned automatically (AutoProvisionExternalUsers, default true) or linked to an existing
// account by email. Examples are commented so the sample runs with no identity provider configured.
//
// To FORCE SSO (hide the username/password form + reject local login), set AllowLocalLogin = false
// in the AddKlassd options callback (the login page keeps the local form when no provider exists,
// so you can't lock yourself out):
//  .AddKlassd(builder.Configuration, o => o.AllowLocalLogin = false)
//
// OpenID Connect / OAuth 2.0 (Okta, Auth0, …) — Newtonsoft-free:
//  var auth = (Klassd.Auth.Abstractions.IAuthBuilder)builder.Services
//      .Last(d => d.ServiceType == typeof(Klassd.Auth.Abstractions.IAuthBuilder)).ImplementationInstance!;
//  auth.AddOpenIdConnect("Company SSO", o =>
//  {
//      o.Authority = builder.Configuration["Oidc:Authority"];
//      o.ClientId = builder.Configuration["Oidc:ClientId"];
//      o.ClientSecret = builder.Configuration["Oidc:ClientSecret"];
//  });
//
// Microsoft Entra ID convenience:
//  auth.AddEntraId(tenantId: "<tenant>", clientId: "<client>", clientSecret: "<secret>");

var app = builder.Build();

app.UseKlassd();  // everything: auth + antiforgery + seed/init + static assets + /api + Blazor admin

app.Run();
