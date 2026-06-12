# Migrating an existing database to Klassd.Auth

As of `Klassd.Auth 0.0.1-beta.1`, Klassd CMS stores users/sessions in the
[Klassd.Auth](https://github.com/getklassd/Klassd.Auth) schema instead of the engine's old
bespoke `users` table. **Fresh installs need nothing** — the app creates the new schema on first
run. **Existing databases need a one-time migration**, because Klassd.Auth owns a table also named
`users` (different shape) and creates it with `CREATE TABLE IF NOT EXISTS`, so on an upgraded
database the new schema would never be created and sign-in would break.

These scripts move the legacy table aside and copy the data across.

## What gets migrated

| Legacy `users` column | Goes to |
|---|---|
| `id` | `users.id` (unchanged — preserves preferences, authorship, any FK by user id) |
| `username` | `users.username` |
| `email` | `users.primary_email` + each login method's `email` |
| `disabled` | `users.disabled` |
| `password_hash` (`salt:hash`) | a `login_methods` row, `kind=0` (EmailPassword), reformatted to `pbkdf2$100000$salt$hash` |
| `provider` + `external_id` (when `provider <> 'local'`) | a `login_methods` row, `kind=1` (ThirdParty) |
| `roles` (JSON array) | `user_metadata` `"roles"` section (skipped when empty) |

**Passwords keep working — no resets.** Legacy hashes are PBKDF2-HMAC-SHA256 (100k iterations,
32-byte key); Klassd.Auth's verifier reads the iteration count out of the hash string, so the only
change needed is the string format (`salt:hash` → `pbkdf2$100000$salt$hash`).

**"No roles" still means Administrator.** The CMS treats a user with no roles as Administrator
(back-compat). The scripts skip writing a roles section for users whose `roles` was empty/`[]`, so
that convention is preserved.

User **preferences** (theme/locale) are untouched — they key on the user id, which is unchanged.

## Run it (once, before starting the upgraded app)

**Back up the database first**, then run the script for your storage adapter. Each script is
transactional and aborts if it detects it has already run.

```bash
# SQLite
sqlite3 klassd.db < migrate-sqlite.sql

# PostgreSQL
psql "Host=…;Database=…;Username=…;Password=…" -f migrate-postgres.sql

# MongoDB — edit DB_NAME / LEGACY_USERS at the top of the script first
mongosh "mongodb://…" migrate-mongodb.js
```

The legacy table/collection is **kept as a backup** (`users_legacy`). Drop it once you've confirmed
sign-in works:

```sql
DROP TABLE users_legacy;                     -- SQLite / Postgres
```
```js
db.getSiblingDB('<db>').users_legacy.drop()  // MongoDB
```

## Notes

- Run this **before** the upgraded app starts (it must create the schema before Klassd.Auth's
  `IF NOT EXISTS` initializer runs). If the app already started against the old DB it will have
  failed to create its schema, not corrupted anything — stop it, migrate, then start.
- The scripts create only the tables they populate (`users`, `login_methods`, `user_metadata`); the
  app creates `sessions`, `signing_keys`, and `email_verification_tokens` on first run.
- Set a real `Klassd:Auth:SigningKey` (32+ bytes) in production config — the engine falls back to a
  shared dev key if it's unset.

## Klassd.Workflows

Klassd.Workflows has the same change, but its legacy table is named **`workflow_users`** — a
*different* name from Klassd.Auth's `users`, so **no rename is needed**; the legacy data is simply
left in place and copied across. Workflows users are email-only with no roles, so adapt
`migrate-postgres.sql` / `migrate-sqlite.sql` by:

- selecting from `workflow_users` instead of `users_legacy` (and dropping the `ALTER TABLE … RENAME`),
- omitting `username` (always `NULL`) and the `user_metadata`/roles step.

Everything else (the password reformat, the EmailPassword/ThirdParty `login_methods` split) is
identical. Ask if you want the ready-made Workflows scripts generated too.
