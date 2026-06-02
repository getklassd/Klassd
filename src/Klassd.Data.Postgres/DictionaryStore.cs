using System.Text.Json;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Klassd.Data.Postgres;

/// <summary>
/// Dictionary entry store over raw Npgsql. <c>values</c> is stored as jsonb via System.Text.Json.
/// </summary>
public sealed class DictionaryStore(PostgresContext context) : IDictionaryStore
{
    public async Task<IReadOnlyList<DictionaryEntryRecord>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, values FROM dictionary";
        var list = new List<DictionaryEntryRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task<DictionaryEntryRecord?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT key, values FROM dictionary WHERE key = @k";
        cmd.Parameters.Add(new NpgsqlParameter("k", NpgsqlDbType.Text) { Value = key });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task UpsertAsync(DictionaryEntryRecord entry, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO dictionary (key, values) VALUES (@k, @v)
            ON CONFLICT (key) DO UPDATE SET values = @v
            """;
        cmd.Parameters.Add(new NpgsqlParameter("k", NpgsqlDbType.Text) { Value = entry.Key });
        cmd.Parameters.Add(new NpgsqlParameter("v", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(entry.Values) });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM dictionary WHERE key = @k";
        cmd.Parameters.Add(new NpgsqlParameter("k", NpgsqlDbType.Text) { Value = key });
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    private static DictionaryEntryRecord Map(NpgsqlDataReader r) => new()
    {
        Key = r.GetString(0),
        Values = JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(1)) ?? new(),
    };
}
