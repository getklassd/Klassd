using Klassd.Abstractions.Media;
using Klassd.Auth.OpenIdConnect;
using Klassd.Backoffice;
using Klassd.Backoffice.Modules.Auth;
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
// Each provider adds a "Sign in with …" button to /admin/login. On first sign-in the user is
// provisioned automatically (AutoProvisionExternalUsers, default true) or linked to an existing
// account by email. Disabled users are rejected. Examples are commented so the sample runs with
// no identity provider configured.
//
// To FORCE SSO (hide the username/password form + reject local login), set AllowLocalLogin = false
// in the AddKlassd options callback — it only takes effect once a provider is configured,
// so you can't lock yourself out:
//  .AddKlassd(builder.Configuration, o => o.AllowLocalLogin = false)
//
// OpenID Connect / OAuth 2.0 (Entra ID, Okta, Auth0, Google, …) — Newtonsoft-free:
//  builder.Services.AddKlassd(...)  // (chain onto the call above)
//      .UseOpenIdConnect("Company SSO", builder.Configuration.GetSection("Oidc"));
//      // section keys: Authority, ClientId, ClientSecret, optional Scope[]/ResponseType/SaveTokens
//
// SAML 2.0 — host-wired through the generic seam so no SAML library's dependencies ship in a
// Klassd package. Reference your chosen SAML auth handler, then:
//  builder.Services.AddKlassd(...)
//      .AddExternalLogin("saml", "Company SSO", auth => auth.AddSaml2("saml", o =>
//      {
//          o.SignInScheme = CmsAuthSchemes.External;   // required: engine exchanges this for the admin cookie
//          o.SPOptions.EntityId = new("https://my-cms.example.com/Saml2");
//          o.IdentityProviders.Add(/* IdP metadata */);
//      }));

var app = builder.Build();

app.UseKlassd();  // everything: auth + antiforgery + seed/init + static assets + /api + Blazor admin

app.Run();
