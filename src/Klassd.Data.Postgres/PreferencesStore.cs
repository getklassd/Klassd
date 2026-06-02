using System.Text.Json;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Klassd.Data.Postgres;

/// <summary>User-preferences store. One row per user.</summary>
public sealed class PreferencesStore(PostgresContext context) : IPreferencesStore
{
    public async Task<UserPreferencesRecord?> GetAsync(string userId, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT user_id, selected_locale, collapsed FROM user_preferences WHERE user_id = @u";
        cmd.Parameters.AddWithValue("u", userId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new UserPreferencesRecord
        {
            UserId = reader.GetString(0),
            SelectedLocale = reader.GetString(1),
            Collapsed = JsonSerializer.Deserialize<List<string>>(reader.GetString(2)) ?? new(),
        };
    }

    public async Task UpsertAsync(UserPreferencesRecord prefs, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO user_preferences (user_id, selected_locale, collapsed)
            VALUES (@u, @l, @c)
            ON CONFLICT (user_id)
            DO UPDATE SET selected_locale = EXCLUDED.selected_locale, collapsed = EXCLUDED.collapsed
            """;
        cmd.Parameters.AddWithValue("u", prefs.UserId);
        cmd.Parameters.AddWithValue("l", prefs.SelectedLocale);
        cmd.Parameters.Add(new NpgsqlParameter("c", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(prefs.Collapsed) });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
