# Klassd

A **code-first, NuGet-distributed headless CMS** for .NET. You define your content model —
pages, blocks and property types — as plain **C# classes**. The engine reflects over them to
drive a **Blazor (Interactive Server) admin** at `/admin` and a **headless JSON API** at `/api`.
No content-type designer, no database migrations to hand-write, no JavaScript build step.

> ⚠️ **Beta.** Klassd is in public beta (`0.0.x`). It works and is tested, but the API surface
> may change between releases until `1.0`. Pin your versions and read the release notes when upgrading.

## Install

Install the engine plus one storage adapter (add `--prerelease` while in beta):

```bash
dotnet add package Klassd.Backoffice --prerelease
dotnet add package Klassd.Data.Sqlite --prerelease
```

```csharp
builder.Services
    .AddKlassd(builder.Configuration)                       // discovers your C# content types
    .UseSqlite(builder.Configuration.GetSection("Sqlite")); // or .UseMongoDb / .UsePostgres

var app = builder.Build();
app.UseKlassd();   // auth + antiforgery + seed/init + static assets + /api + Blazor admin
app.Run();
```

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

## Documentation

Full quickstart, screenshots, media/CORS/deployment guides and runnable samples are on the
project's GitHub repository: **https://github.com/getklassd/Klassd**

## License

[MIT](https://github.com/getklassd/Klassd/blob/main/LICENSE) © Mark Lonquist
