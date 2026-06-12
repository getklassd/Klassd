using Klassd.Abstractions.Storage;
using Microsoft.Data.Sqlite;

namespace Klassd.Data.Sqlite;

/// <summary>Runs idempotent DDL once against the single database (before seeding/serving).</summary>
public sealed class SqliteSchemaInitializer(SqliteOptions options, IndexDefinitions indexes) : IStorageInitializer
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
          updated_at TEXT NOT NULL,
          publish_at TEXT NULL,
          unpublish_at TEXT NULL,
          published INTEGER NOT NULL DEFAULT 1);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_pages_locale_slug ON pages (locale_code, slug);
        CREATE INDEX IF NOT EXISTS ix_pages_content ON pages (content_id);
        CREATE INDEX IF NOT EXISTS ix_pages_parent_locale ON pages (parent_id, locale_code);

        CREATE TABLE IF NOT EXISTS page_versions (
          version_id TEXT PRIMARY KEY,
          page_id TEXT NOT NULL,
          content_id TEXT NOT NULL,
          locale_code TEXT NOT NULL,
          status TEXT NOT NULL,
          number INTEGER NOT NULL DEFAULT 0,
          name TEXT NOT NULL,
          slug TEXT NOT NULL,
          data TEXT NOT NULL DEFAULT '{}',
          block_areas TEXT NOT NULL DEFAULT '{}',
          publish_at TEXT NULL,
          unpublish_at TEXT NULL,
          created_at TEXT NOT NULL,
          created_by TEXT NULL);
        CREATE INDEX IF NOT EXISTS ix_page_versions_page ON page_versions (page_id, status);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_page_versions_draft ON page_versions (page_id) WHERE status = 'draft';

        CREATE TABLE IF NOT EXISTS user_preferences (
          user_id TEXT PRIMARY KEY,
          selected_locale TEXT NOT NULL DEFAULT '',
          collapsed TEXT NOT NULL DEFAULT '[]',
          theme TEXT NOT NULL DEFAULT '');

        CREATE TABLE IF NOT EXISTS media (
          id TEXT PRIMARY KEY, section TEXT NOT NULL, key TEXT NOT NULL,
          file_name TEXT NOT NULL, display_name TEXT NULL, content_type TEXT NOT NULL, size INTEGER NOT NULL,
          width INTEGER NULL, height INTEGER NULL, alt_text TEXT NULL,
          focal_points TEXT NOT NULL DEFAULT '[]', data TEXT NOT NULL DEFAULT '{}',
          uploaded_at TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_media_section ON media (section);

        CREATE TABLE IF NOT EXISTS dictionary (
          key TEXT PRIMARY KEY,
          "values" TEXT NOT NULL);

        CREATE TABLE IF NOT EXISTS globals (
          type_name TEXT NOT NULL,
          locale_code TEXT NOT NULL,
          data TEXT NOT NULL DEFAULT '{}',
          block_areas TEXT NOT NULL DEFAULT '{}',
          updated_at TEXT NOT NULL,
          PRIMARY KEY (type_name, locale_code));

        CREATE TABLE IF NOT EXISTS settings (
          key TEXT PRIMARY KEY,
          value TEXT NOT NULL);
        """;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(options.ConnectionString);
        await conn.OpenAsync(cancellationToken);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = Ddl;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Idempotent column additions for databases created before the column existed
        // (CREATE TABLE IF NOT EXISTS above won't alter a pre-existing table).
        await AddColumnIfMissingAsync(conn, "user_preferences", "theme", "TEXT NOT NULL DEFAULT ''", cancellationToken);
        await AddColumnIfMissingAsync(conn, "media", "display_name", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(conn, "pages", "publish_at", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(conn, "pages", "unpublish_at", "TEXT NULL", cancellationToken);
        // Pre-versioning rows were always live, so default existing pages to published.
        await AddColumnIfMissingAsync(conn, "pages", "published", "INTEGER NOT NULL DEFAULT 1", cancellationToken);

        // Generated indexes from [Indexable] content fields + media built-in columns (idempotent).
        foreach (var ix in indexes.JsonIndexes)
            await ExecAsync(conn,
                $"CREATE INDEX IF NOT EXISTS ix_{ix.Table}_{Sanitize(ix.Key)} ON {ix.Table} (json_extract({ix.JsonColumn}, '$.{ix.Key}'))",
                cancellationToken);
        foreach (var ix in indexes.ColumnIndexes)
            await ExecAsync(conn,
                $"CREATE INDEX IF NOT EXISTS ix_{ix.Table}_{ix.SqlColumn} ON {ix.Table} ({ix.SqlColumn})",
                cancellationToken);
    }

    private static async Task ExecAsync(SqliteConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // Index NAME hardening only; keys are compile-time C# property names (safe to interpolate into SQL).
    private static string Sanitize(string key) =>
        new(key.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    private static async Task AddColumnIfMissingAsync(
        SqliteConnection conn, string table, string column, string definition, CancellationToken ct)
    {
        await using (var check = conn.CreateCommand())
        {
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = @c";
            check.Parameters.AddWithValue("@c", column);
            if (Convert.ToInt64(await check.ExecuteScalarAsync(ct)) > 0)
                return;
        }
        await using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await alter.ExecuteNonQueryAsync(ct);
    }
}
