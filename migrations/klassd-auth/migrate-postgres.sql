-- Klassd.Auth migration — PostgreSQL
-- =============================================================================
-- Migrates a legacy Klassd CMS `users` table (id, username, email, password_hash,
-- provider, external_id, disabled, roles jsonb) into the Klassd.Auth schema
-- (`users` + `login_methods` + `user_metadata`).
--
-- See migrate-sqlite.sql for the rationale. Run ONCE, BEFORE starting the
-- upgraded app, against the CMS database. Take a backup first (pg_dump). The
-- script is transactional and guards against double-runs.
--
-- Passwords are preserved: legacy `salt:hash` (PBKDF2-HMAC-SHA256, 100k iters,
-- 32-byte key) -> Klassd.Auth `pbkdf2$100000$salt$hash` (iteration count is read
-- from the string, so logins keep working — no resets).
--
--   psql "<connection string>" -f migrate-postgres.sql
-- =============================================================================

BEGIN;

DO $$
BEGIN
    IF to_regclass('public.login_methods') IS NOT NULL THEN
        RAISE EXCEPTION 'login_methods already exists — looks already migrated; aborting';
    END IF;
END $$;

-- 1) Move the legacy table aside.
ALTER TABLE users RENAME TO users_legacy;

-- 2) Create the Klassd.Auth tables this script populates (exact DDL match; the
--    app creates sessions/signing_keys/email_verification_tokens on first run).
CREATE TABLE users (
    id            text PRIMARY KEY,
    username      text,
    primary_email text,
    disabled      boolean NOT NULL DEFAULT false,
    created_at    timestamptz NOT NULL
);
CREATE UNIQUE INDEX ux_users_username ON users(username) WHERE username IS NOT NULL;
CREATE INDEX ix_users_email ON users(primary_email);

CREATE TABLE login_methods (
    id               text PRIMARY KEY,
    user_id          text NOT NULL,
    kind             int  NOT NULL,   -- 0 = EmailPassword, 1 = ThirdParty
    email            text,
    email_verified   boolean NOT NULL DEFAULT false,
    password_hash    text,
    provider_id      text,
    provider_user_id text,
    created_at       timestamptz NOT NULL
);
CREATE INDEX ix_lm_user     ON login_methods(user_id);
CREATE INDEX ix_lm_email    ON login_methods(kind, email);
CREATE INDEX ix_lm_provider ON login_methods(provider_id, provider_user_id);

CREATE TABLE user_metadata (
    user_id text PRIMARY KEY,
    json    jsonb NOT NULL
);

-- 3) Users.
INSERT INTO users (id, username, primary_email, disabled, created_at)
SELECT id, username, email, COALESCE(disabled, false), now()
FROM users_legacy;

-- 4) Local (password) login methods.
INSERT INTO login_methods (id, user_id, kind, email, email_verified, password_hash, provider_id, provider_user_id, created_at)
SELECT replace(gen_random_uuid()::text, '-', ''), id, 0, email, false,
       'pbkdf2$100000$' || replace(password_hash, ':', '$'),
       NULL, NULL, now()
FROM users_legacy
WHERE password_hash IS NOT NULL AND password_hash <> '';

-- 5) External (SSO) login methods.
INSERT INTO login_methods (id, user_id, kind, email, email_verified, password_hash, provider_id, provider_user_id, created_at)
SELECT replace(gen_random_uuid()::text, '-', ''), id, 1, email, true,
       NULL, provider, external_id, now()
FROM users_legacy
WHERE provider IS NOT NULL AND provider <> 'local'
  AND external_id IS NOT NULL AND external_id <> '';

-- 6) Roles -> user_metadata "roles" section (skip empty: no roles == Administrator).
INSERT INTO user_metadata (user_id, json)
SELECT id, jsonb_build_object('roles', roles)
FROM users_legacy
WHERE roles IS NOT NULL AND roles <> '[]'::jsonb;

-- 7) users_legacy is kept as a backup. Drop it once login is verified:
--    DROP TABLE users_legacy;

COMMIT;
