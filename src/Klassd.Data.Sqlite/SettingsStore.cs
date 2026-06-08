using Klassd.Abstractions.Storage;

namespace Klassd.Data.Sqlite;

/// <summary>Key/value system settings store over raw Microsoft.Data.Sqlite (<c>settings</c> table).</summary>
public sealed class SettingsStore(SqliteContext context) : ISettingsStore
{
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = @k";
        cmd.Parameters.AddWithValue("@k", key);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value) VALUES (@k, @v)
            ON CONFLICT(key) DO UPDATE SET value = @v
            """;
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM settings WHERE key = @k";
        cmd.Parameters.AddWithValue("@k", key);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
}
