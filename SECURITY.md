# Security Policy

## Reporting a vulnerability

Please report security vulnerabilities **privately** — do not open a public issue.

Use GitHub's [**private vulnerability reporting**](https://github.com/getklassd/Klassd/security/advisories/new)
(Security → Report a vulnerability). We aim to acknowledge reports within a few business days and
will keep you updated on remediation progress. Please give us a reasonable window to release a fix
before any public disclosure.

When reporting, include where possible:

- Affected package(s) and version(s)
- A description of the issue and its impact
- Steps to reproduce or a proof of concept
- Any suggested remediation

## Supported versions

Klassd is pre-1.0. Security fixes are applied to the latest released version. Until 1.0, please
upgrade to the newest version to receive fixes.

## Things that are intentional (not vulnerabilities)

Klassd is a **headless CMS**, and some behavior is by design:

- **Public content delivery.** The `GET /api/pages*`, `/api/dictionary/resolved/{locale}` and
  `/api/media/{id}` endpoints are **anonymous by design** so a public frontend can read published
  content. Do not place gated/confidential content in delivery. See the README for the optional
  server-side API-key gate (for SSR/SSG/BFF only — never for browser/SPA callers).
- **CORS is not authorization.** The CORS allow-list only restricts cross-origin *browser* requests;
  it does not protect against server-side callers. It is not an auth boundary.

If you believe any of the above can be abused beyond its documented intent (e.g. management/write
endpoints reachable without the admin cookie, auth bypass, SSRF, injection), that **is** a security
issue — please report it.

## Scope

In scope: all `Klassd.*` packages and the engine in this repository. Out of scope: third-party
storage/media/identity providers you wire up (MongoDB, PostgreSQL, S3, GCS, your OIDC/SAML IdP) —
report those to their respective maintainers.
