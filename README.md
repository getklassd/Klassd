# Klassd

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![CI](https://github.com/getklassd/Klassd/actions/workflows/ci.yml/badge.svg)](https://github.com/getklassd/Klassd/actions/workflows/ci.yml)

A **code-first, NuGet-distributed headless CMS** for .NET. You define your content model —
pages, blocks and property types — as plain **C# classes**. The engine reflects over them to
drive a **Blazor (Interactive Server) admin** at `/admin` and a **headless JSON API** at `/api`.
No content-type designer, no database migrations to hand-write, no JavaScript build step.

> Your content schema lives in your codebase, versioned with your app and refactored with your IDE.

## Why Klassd

- **Code-first** — content types are C# classes; rename a property in your IDE, not a CMS UI.
- **Headless** — public JSON delivery API; render with any frontend (or none).
- **Pluggable storage** — MongoDB, PostgreSQL or SQLite via a single `.UseXxx(...)` call.
- **Pluggable media** — file system, Amazon S3 or Google Cloud Storage, with named sections.
- **Localization built in** — per-locale fields via `[Localized]`, market-local scheduling.
- **No JS toolchain** — the admin is Blazor; cloud SDKs stay isolated in their own packages.

## Quickstart

Install the engine plus one storage adapter:

```bash
dotnet add package Klassd.Backoffice
dotnet add package Klassd.Data.Sqlite
```

**1. Define content types as C# classes** (discovered automatically from your app's assembly):

```csharp
using Klassd.Core.Abstractions;

[CmsPage(DefaultSlug = "")]
[AllowedChildren(typeof(ContentPage))]
public class HomePage : PageBase
{
    [Localized]                       // separate value per locale
    public string Title { get; set; } = "";
    public string SubTitle { get; set; } = "";
    public BlockArea HeroBlocks { get; set; } = new();
}

public class HeroBlock : BlockBase
{
    public string Heading { get; set; } = "";

    [CmsField(FieldType = "media")]   // media picker; stores the media item id
    public string Image { get; set; } = "";
}
```

**2. Wire it up** in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddKlassd(builder.Configuration)                              // discovers your content types
    .UseSqlite(builder.Configuration.GetSection("Sqlite"))         // or .UseMongoDb / .UsePostgres
    .UseInMemoryCache();                                           // optional read-through cache

var app = builder.Build();
app.UseKlassd();   // auth + antiforgery + seed/init + static assets + /api + Blazor admin
app.Run();
```

```jsonc
// appsettings.json
"Sqlite": { "ConnectionString": "Data Source=klassd.db" }
```

**3. Run** and open `/admin`. The public site reads published content from `/api/pages`.

See [`src/Klassd.Sample`](src/Klassd.Sample) for a complete runnable host with multiple page/block
types, a custom property editor, media sections and SSO examples.

## Media

Media is organized into named **sections**, each backed by its own **blob adapter**. Add a section
with `.AddMedia(...)` after choosing storage, and reference it from a field with
`[CmsField(FieldType = "media")]` (the admin renders an upload + picker; the field stores the item id):

```bash
dotnet add package Klassd.Media.FileSystem   # and/or .Media.S3, .Media.GoogleCloud
```

```csharp
builder.Services
    .AddKlassd(builder.Configuration)
    .UseSqlite(builder.Configuration.GetSection("Sqlite"))
    .AddMedia(media =>
    {
        // Local disk — longest image edge downscaled to 2000px in-browser before upload
        media.AddSection("images", s => s
            .UseFileSystem(Path.Combine(builder.Environment.ContentRootPath, "media", "images"))
            .AllowContentTypes("image/*")
            .ResizeImages(2000));

        // Amazon S3 (or any S3-compatible backend via ServiceUrl/ForcePathStyle)
        media.AddSection("documents", s => s
            .UseS3(o =>
            {
                o.Bucket = "my-cms-docs";
                o.Region = "eu-west-1";   // omit AccessKey/SecretKey to use the default AWS credential chain
            })
            .AllowContentTypes("application/pdf"));

        // Google Cloud Storage
        media.AddSection("video", s => s
            .UseGoogleCloudStorage(o =>
            {
                o.Bucket = "my-cms-video";
                o.CredentialsPath = "/secrets/gcs.json";   // or CredentialsJson, or ambient ADC
            }));
    });
```

Each section is independent: mix local disk, S3 and GCS in the same app, set per-section allowed
content types, and downscale images on the client with `ResizeImages(maxEdgePixels)`.

> **Need a backend we don't ship** (Azure Blob, an in-house store, …)? A media adapter is just an
> `IBlobStore` (three methods) plus a `UseXxx` extension. See
> [`examples/InMemoryMediaAdapter`](examples/InMemoryMediaAdapter) for a complete, annotated walkthrough.

## Custom adapters

Klassd's storage and media backends are swappable extension points — the engine depends only on
interfaces in `Klassd.Abstractions`, never on a concrete database or cloud SDK. To target a backend
we don't ship, implement the relevant interface and add a `UseXxx` registration extension. Worked,
compilable examples live in [`examples/`](examples):

| Example | Implements | Extension point |
|---------|-----------|-----------------|
| [`InMemoryMediaAdapter`](examples/InMemoryMediaAdapter) | `IBlobStore` | `UseInMemoryBlobs()` on a media section |
| [`InMemoryStorageAdapter`](examples/InMemoryStorageAdapter) | `IPageStore`, `IMediaStore`, `IDictionaryStore`, `IUserStore`, `IPreferencesStore`, `IUnitOfWork`, `IStorageInitializer` | `UseInMemoryStorage()` on the CMS builder |

## Content delivery & CORS

The headless **GET delivery** endpoints are **anonymous** so a public frontend can read published
content without credentials:

- `GET /api/pages`, `/api/pages/{id}`, `/api/pages/content/{contentId}`, `/api/pages/{id}/translations`
- `GET /api/dictionary/resolved/{locale}`
- `GET /api/media/{id}`

Everything else — page/media/dictionary **management**, users, preferences, and the `/admin` UI — still
requires the admin cookie.

Restrict which browser origins may fetch via JS with config (empty/unset ⇒ any origin):

```jsonc
"Klassd": {
  "Cors": { "AllowedOrigins": [ "https://www.example.com", "https://shop.example.com" ] }
}
```

> CORS only limits cross-origin **browser** requests; it is not an authorization boundary for
> server-side callers. Delivery content is genuinely public — don't put gated content in it.

### Optional: gate delivery with an API key

This is a headless CMS, so the rendering model is the consumer's choice. If your frontend renders
**server-side** (SSR/SSG/BFF), you can require an API key on the delivery GETs — the key stays on
your server, never in the browser:

```jsonc
"Klassd": {
  "Delivery": { "RequireApiKey": true, "ApiKey": "<long-random-secret>", "ApiKeyHeader": "X-Api-Key" }
}
```

Callers then send `X-Api-Key: <secret>`; requests without it get `401`. Default is **off** (public).

> Do **not** enable this for a browser/SPA that calls the CMS directly — the key would ship in the
> client bundle and provide no real protection. For a client-side app that needs gating, put a
> backend-for-frontend (BFF) in front that holds the key, or use per-user auth.

## Packages

| Package | Purpose |
|---------|---------|
| `Klassd.Abstractions` | Storage adapter interfaces + DB-agnostic POCOs (no deps) |
| `Klassd.Core` | Content base types, attributes, registries, localization, default property types |
| `Klassd.Backoffice` | The engine: `AddKlassd`/`UseKlassd`, Blazor admin, headless `/api` |
| `Klassd.Data.MongoDb` / `.Data.Postgres` / `.Data.Sqlite` | Storage adapters |
| `Klassd.Cache.InMemory` / `.Cache.Redis` | Read-through page cache adapters |
| `Klassd.Media.FileSystem` / `.Media.S3` / `.Media.GoogleCloud` | Media blob adapters |
| `Klassd.Auth.OpenIdConnect` | OIDC/OAuth SSO for the backoffice (SAML via the generic seam) |

The engine package carries **no** MongoDB/AWS/Google dependency — each adapter keeps its SDK
isolated, so you only pull in what you wire up.

## Deployment notes

### Time zones (content scheduling)

Block scheduling is **market-local**: each locale carries an IANA `TimeZone` (e.g. `Europe/Berlin`,
`Asia/Dubai`) and editors author schedule times as wall-clock time in that market. Times are stored
and compared in UTC, so delivery is correct across markets simultaneously.

Resolving IANA time zones requires the **OS time-zone database**:

- **Debian/Ubuntu** base images (incl. the default `mcr.microsoft.com/dotnet/aspnet`) include it — nothing to do.
- **Alpine** images do not. Add it:

  ```dockerfile
  RUN apk add --no-cache tzdata
  ```

- **Chiseled / distroless**: use the `-extra` image variant (ships ICU + tz data), not the bare one.

If a configured locale time zone can't be resolved at startup, the engine logs a **warning**
(category `Klassd.Scheduling`) naming the locale and zone, and falls back to UTC for that
market (so scheduling would be offset-wrong). Watch for that warning when deploying to slim images.

> Note: this is about the time-zone database, not `InvariantGlobalization` — leaving globalization
> invariant does not remove the tz-data requirement.

## Building & testing

```bash
dotnet build Klassd.slnx -c Release
```

Tests use [TUnit](https://tunit.dev/) on the Microsoft.Testing.Platform. On the .NET 10 SDK,
`dotnet test` does not work for these — run each project directly:

```bash
dotnet run --project tests/Klassd.UnitTests -c Release
dotnet run --project tests/Klassd.IntegrationTests -c Release   # needs Docker (Testcontainers); container tests auto-skip without it
dotnet run --project tests/Klassd.UiTests -c Release            # needs Playwright browsers (see below)
```

UI tests are Playwright E2E; first run installs the browser:

```bash
pwsh tests/Klassd.UiTests/bin/Release/net10.0/playwright.ps1 install chromium
```

## Security

See [SECURITY.md](SECURITY.md) for the vulnerability reporting process and important notes on the
public delivery endpoints.

## License

[MIT](LICENSE) © Mark Lonquist
