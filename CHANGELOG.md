# Changelog

All notable changes to Klassd are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
While Klassd is in beta (`0.0.x`), the public API may change between releases.

## [Unreleased]

## [0.0.1-beta.1] - 2026-06-04

First public beta. Distributed as 12 NuGet packages (engine + pluggable adapters);
see the [README](README.md) for the full quickstart.

### Added

- **Code-first content model** — define pages, blocks and property types as plain C#
  classes; the engine reflects over them to drive the admin and API. Attributes for
  page/block types, allowed children, fields and default slugs.
- **Blazor admin** at `/admin` (Interactive Server) — page tree with per-page-type
  icons, field/block editor, users, and a dictionary editor, with a per-user light/dark
  theme persisted to preferences. daisyUI-based styling, no JS build step.
- **Headless JSON delivery API** at `/api` — anonymous published-content GETs, with
  source-generated `System.Text.Json`, configurable CORS, and an optional API-key gate
  for server-side (SSR/BFF) callers.
- **Pluggable storage** — `Klassd.Data.MongoDb`, `Klassd.Data.Postgres`,
  `Klassd.Data.Sqlite` via a single `.UseXxx(...)` call.
- **Pluggable media** — named sections backed by `Klassd.Media.FileSystem`,
  `Klassd.Media.S3` (S3-compatible) or `Klassd.Media.GoogleCloud`; media library with
  modal edit, folders, bulk/drag-and-drop upload, in-browser image downscaling, and
  per-section focal-point breakpoints.
- **Read-through page cache** — `Klassd.Cache.InMemory` and `Klassd.Cache.Redis`.
- **Localization** — per-locale fields via `[Localized]`, optional locale labels, and
  market-local (IANA time zone) block scheduling stored/compared in UTC.
- **Globals** — singleton content types plus page-tree navigation metadata.
- **`[Indexable]`** — auto-generated storage indexes, plus admin media/global search.
- **SSO** — `Klassd.Auth.OpenIdConnect` for OIDC/OAuth backoffice login; SAML via the
  generic external-login seam.
- **Custom adapters** — storage and media are extension points over interfaces in
  `Klassd.Abstractions`; worked examples under [`examples/`](examples).

[Unreleased]: https://github.com/getklassd/Klassd/compare/v0.0.1-beta.1...HEAD
[0.0.1-beta.1]: https://github.com/getklassd/Klassd/releases/tag/v0.0.1-beta.1
