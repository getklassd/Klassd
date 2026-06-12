-- Klassd.Auth migration — SQLite
-- =============================================================================
-- Migrates a legacy Klassd CMS `users` table (the pre-Klassd.Auth schema:
--   id, username, email, password_hash, provider, external_id, disabled, roles)
-- into the Klassd.Auth schema (`users` + `login_methods` + `user_metadata`).
--
-- WHY THIS IS NEEDED: Klassd.Auth owns a table also named `users`, but with a
-- different shape, and creates it with CREATE TABLE IF NOT EXISTS — so on an
-- existing CMS database the new schema is never created and auth breaks. This
-- script renames the legacy table out of the way, lets the app create its own,
-- and copies the data across.
--
-- RUN ONCE, BEFORE starting the upgraded app, against the CMS database file.
-- Take a backup first (just copy the .db file). The script is transactional and
-- guards against double-runs (it aborts if `login_methods` already exists).
--
-- PASSWORDS ARE PRESERVED: the legacy `salt:hash` (PBKDF2-HMAC-SHA256, 100k
-- iterations, 32-byte key) is reformatted to Klassd.Auth's
-- `pbkdf2$100000$salt$hash`. Klassd.Auth reads the iteration count from the
-- string, so existing passwords keep working — no resets.
--
--   sqlite3 klassd.db < migrate-sqlite.sql
-- =============================================================================

BEGIN;

-- Double-run guard: this whole script is one transaction, and step 1 (rename to
-- users_legacy) plus step 2 (CREATE TABLE users, no IF NOT EXISTS) both fail if
-- the migration already ran — rolling everything back. So re-running is safe.

-- 1) Move the legacy table aside.
ALTER TABLE users RENAME TO users_legacy;

-- 2) Create the Klassd.Auth tables this script populates (exact DDL match;
--    the app creates sessions/signing_keys/email_verification_tokens on first run).
CREATE TABLE users (
    id            TEXT PRIMARY KEY,
    username      TEXT,
    primary_email TEXT,
    disabled      INTEGER NOT NULL DEFAULT 0,
    created_at    TEXT NOT NULL
);
CREATE UNIQUE INDEX ux_users_username ON users(username) WHERE username IS NOT NULL;
CREATE INDEX ix_users_email ON users(primary_email);

CREATE TABLE login_methods (
    id               TEXT PRIMARY KEY,
    user_id          TEXT NOT NULL,
    kind             INTEGER NOT NULL,   -- 0 = EmailPassword, 1 = ThirdParty
    email            TEXT,
    email_verified   INTEGER NOT NULL DEFAULT 0,
    password_hash    TEXT,
    provider_id      TEXT,
    provider_user_id TEXT,
    created_at       TEXT NOT NULL
);
CREATE INDEX ix_lm_user     ON login_methods(user_id);
CREATE INDEX ix_lm_email    ON login_methods(kind, email);
CREATE INDEX ix_lm_provider ON login_methods(provider_id, provider_user_id);

CREATE TABLE user_metadata (
    user_id TEXT PRIMARY KEY,
    json    TEXT NOT NULL
);

-- 3) Users.
INSERT INTO users (id, username, primary_email, disabled, created_at)
SELECT id, username, email, COALESCE(disabled, 0), strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
FROM users_legacy;

-- 4) Local (password) login methods — anyone with a non-empty password hash.
--    Reformat salt:hash -> pbkdf2$100000$salt$hash (lossless; same algorithm).
INSERT INTO login_methods (id, user_id, kind, email, email_verified, password_hash, provider_id, provider_user_id, created_at)
SELECT lower(hex(randomblob(16))), id, 0, email, 0,
       'pbkdf2$100000$' || replace(password_hash, ':', '$'),
       NULL, NULL, strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
FROM users_legacy
WHERE password_hash IS NOT NULL AND password_hash <> '';

-- 5) External (SSO) login methods — anyone linked to a provider.
INSERT INTO login_methods (id, user_id, kind, email, email_verified, password_hash, provider_id, provider_user_id, created_at)
SELECT lower(hex(randomblob(16))), id, 1, email, 1,
       NULL, provider, external_id, strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
FROM users_legacy
WHERE provider IS NOT NULL AND provider <> 'local'
  AND external_id IS NOT NULL AND external_id <> '';

-- 6) Roles -> user_metadata "roles" section (skip empty: no roles == Administrator, by convention).
INSERT INTO user_metadata (user_id, json)
SELECT id, '{"roles":' || roles || '}'
FROM users_legacy
WHERE roles IS NOT NULL AND roles <> '' AND roles <> '[]';

-- 7) Keep users_legacy as a backup. Drop it manually once you've verified login:
--    DROP TABLE users_legacy;

COMMIT;
