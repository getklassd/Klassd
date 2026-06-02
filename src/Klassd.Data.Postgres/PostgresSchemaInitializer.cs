using Klassd.Abstractions.Storage;

namespace Klassd.Data.Postgres;

/// <summary>Runs idempotent DDL once against the single data source (before seeding/serving).</summary>
public sealed class PostgresSchemaInitializer(INpgsqlDataSourceProvider provider) : IStorageInitializer
{
    private const string Ddl = """
        CREATE TABLE IF NOT EXISTS pages (
          id text PRIMARY KEY,
          content_id text NOT NULL,
          locale_code text NOT NULL,
          parent_id text NULL,
          page_type text NOT NULL,
          name text NOT NULL,
          slug text NOT NULL,
          data jsonb NOT NULL DEFAULT '{}',
          block_areas jsonb NOT NULL DEFAULT '{}',
          created_at timestamptz NOT NULL,
          updated_at timestamptz NOT NULL);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_pages_locale_slug ON pages (locale_code, slug);
        CREATE INDEX IF NOT EXISTS ix_pages_content ON pages (content_id);
        CREATE INDEX IF NOT EXISTS ix_pages_parent_locale ON pages (parent_id, locale_code);

        CREATE TABLE IF NOT EXISTS users (
          id text PRIMARY KEY, username text NOT NULL, email text NULL,
          password_hash text NOT NULL, provider text NOT NULL DEFAULT 'local',
          external_id text NULL, disabled boolean NOT NULL DEFAULT false);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_users_username ON users (username);

        CREATE TABLE IF NOT EXISTS user_preferences (
          user_id text PRIMARY KEY,
          selected_locale text NOT NULL DEFAULT '',
          collapsed jsonb NOT NULL DEFAULT '[]',
          theme text NOT NULL DEFAULT '');
        ALTER TABLE user_preferences ADD COLUMN IF NOT EXISTS theme text NOT NULL DEFAULT '';

        CREATE TABLE IF NOT EXISTS media (
          id text PRIMARY KEY, section text NOT NULL, key text NOT NULL,
          file_name text NOT NULL, display_name text NULL, content_type text NOT NULL, size bigint NOT NULL,
          width int NULL, height int NULL, alt_text text NULL,
          focal_points jsonb NOT NULL DEFAULT '[]', data jsonb NOT NULL DEFAULT '{}',
          uploaded_at timestamptz NOT NULL);
        ALTER TABLE media ADD COLUMN IF NOT EXISTS display_name text NULL;
        CREATE INDEX IF NOT EXISTS ix_media_section ON media (section);

        CREATE TABLE IF NOT EXISTS dictionary (
          key text PRIMARY KEY,
          values jsonb NOT NULL);

        CREATE TABLE IF NOT EXISTS globals (
          type_name text NOT NULL,
          locale_code text NOT NULL,
          data jsonb NOT NULL DEFAULT '{}',
          block_areas jsonb NOT NULL DEFAULT '{}',
          updated_at timestamptz NOT NULL,
          PRIMARY KEY (type_name, locale_code));
        """;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await provider.DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = Ddl;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
