# Klassd

A code-first, NuGet-distributed headless CMS. Content types (pages, blocks, property types) are
defined as C# classes in the consuming app; the engine reflects over them to drive a Blazor
(Interactive Server) admin at `/admin` and a headless JSON API at `/api`.

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
