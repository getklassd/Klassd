# Changelog

All notable changes to Klassd are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
While Klassd is in beta (`0.0.x`), the public API may change between releases.

## [Unreleased]

## [0.0.1-beta.2] - 2026-06-08

### Added

- **Anonymous usage telemetry** — `Klassd.Backoffice` sends one opt-out snapshot on startup (engine
  version, runtime, which storage/cache/media adapters and features are enabled, and aggregate type
  counts — no content, secrets or hostnames). Disable via `Klassd:Telemetry:Enabled=false`, the admin
  **Settings** page, or `KLASSD_TELEMETRY_OPTOUT=1`. Install id + opt-out persist via the new durable
  `ISettingsStore` so they survive stateless replicas.
- **Drafts & versioning** — draft-first editing (edits don't go live until published), publish/
  unpublish/discard, immutable version history with rollback, and per-page scheduled publishing
  (`PublishAt`/`UnpublishAt`). Configurable history retention (`Klassd:Versioning:HistoryLimit`).
- **Roles & permissions** — capability-based authorization with built-in Administrator/Editor/Author
  roles (multiple per user); enforced on the management API and reflected in the admin UI.
- **Full-text search** — `Klassd.Search.Lucene`, an opt-in storage-agnostic Lucene.NET index, kept
  live via content events and rebuilt from the database on startup.
- **Webhooks** — `Klassd.Webhooks`, opt-in HMAC-signed delivery of content-change events.
- **In-process notifications** — synchronous, ordered, cancelable hooks (`PageSaving`/`PageSaved`,
  `PagePublishing`/`PagePublished`, `PageUnpublishing`/`PageUnpublished`, `PageDeleting`/`PageDeleted`)
  that can mutate the entity or cancel the operation; `AddNotificationHandler<,>()`.
- **GraphQL** — `Klassd.GraphQL`, an opt-in read-only GraphQL delivery API over HotChocolate (not in core).
- **HybridCache** — `Klassd.Cache.Hybrid`, an L1+L2 read-through cache over Microsoft.Extensions.Caching.Hybrid.
- **Rich text + more field types** — a `richtext` editor (Quill) plus `email`/`url`/`color`/`decimal`/
  `date`/`time` built-in property types.
- **Delivery ergonomics** — `GET /api/pages/by-slug/{**slug}` and reference URL resolution via
  `?depth`/`?expand` on single-page GETs.

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

[Unreleased]: https://github.com/getklassd/Klassd/compare/v0.0.1-beta.2...HEAD
[0.0.1-beta.2]: https://github.com/getklassd/Klassd/compare/v0.0.1-beta.1...v0.0.1-beta.2
[0.0.1-beta.1]: https://github.com/getklassd/Klassd/releases/tag/v0.0.1-beta.1
