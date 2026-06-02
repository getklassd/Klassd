using Klassd.Abstractions.Storage;
using Microsoft.Data.Sqlite;

namespace Klassd.Data.Sqlite;

/// <summary>Runs idempotent DDL once against the single database (before seeding/serving).</summary>
public sealed class SqliteSchemaInitializer(SqliteOptions options) : IStorageInitializer
{
    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS pages (
          id TEXT PRIMARY KEY,
          content_id TEXT NOT NULL,
          locale_code TEXT NOT NULL,
          parent_id TEXT NULL,
          page_type TEXT NOT NULL,
          name TEXT NOT NULL,
          slug TEXT NOT NULL,
          data TEXT NOT NULL DEFAULT '{}',
          block_areas TEXT NOT NULL DEFAULT '{}',
          created_at TEXT NOT NULL,
          updated_at TEXT NOT NULL);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_pages_locale_slug ON pages (locale_code, slug);
        CREATE INDEX IF NOT EXISTS ix_pages_content ON pages (content_id);
        CREATE INDEX IF NOT EXISTS ix_pages_parent_locale ON pages (parent_id, locale_code);

        CREATE TABLE IF NOT EXISTS users (
          id TEXT PRIMARY KEY, username TEXT NOT NULL, password_hash TEXT NOT NULL,
          email TEXT NULL, provider TEXT NOT NULL DEFAULT 'local', external_id TEXT NULL,
          disabled INTEGER NOT NULL DEFAULT 0);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_users_username ON users (username);

        CREATE TABLE IF NOT EXISTS user_preferences (
          user_id TEXT PRIMARY KEY,
          selected_locale TEXT NOT NULL DEFAULT '',
          collapsed TEXT NOT NULL DEFAULT '[]');

        CREATE TABLE IF NOT EXISTS media (
          id TEXT PRIMARY KEY, section TEXT NOT NULL, key TEXT NOT NULL,
          file_name TEXT NOT NULL, content_type TEXT NOT NULL, size INTEGER NOT NULL,
          width INTEGER NULL, height INTEGER NULL, alt_text TEXT NULL,
          focal_points TEXT NOT NULL DEFAULT '[]', data TEXT NOT NULL DEFAULT '{}',
          uploaded_at TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_media_section ON media (section);

        CREATE TABLE IF NOT EXISTS dictionary (
          key TEXT PRIMARY KEY,
          "values" TEXT NOT NULL);
        """;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(options.ConnectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = Ddl;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
