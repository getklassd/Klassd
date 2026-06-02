using System.Text.Json;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Klassd.Data.Postgres;

/// <summary>Singleton-content store. One row per (type_name, locale_code); data + block_areas as jsonb.</summary>
public sealed class GlobalStore(PostgresContext context) : IGlobalStore
{
    public async Task<GlobalRecord?> GetAsync(string typeName, string localeCode, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT type_name, locale_code, data, block_areas, updated_at FROM globals WHERE type_name = @t AND locale_code = @l";
        cmd.Parameters.AddWithValue("t", typeName);
        cmd.Parameters.AddWithValue("l", localeCode);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new GlobalRecord
        {
            TypeName = r.GetString(0),
            LocaleCode = r.GetString(1),
            Data = JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(2)) ?? new(),
            BlockAreas = JsonSerializer.Deserialize<Dictionary<string, List<BlockInstanceRecord>>>(r.GetString(3)) ?? new(),
            UpdatedAt = r.GetDateTime(4),
        };
    }

    public async Task UpsertAsync(GlobalRecord g, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO globals (type_name, locale_code, data, block_areas, updated_at)
            VALUES (@t, @l, @data, @blocks, @updated)
            ON CONFLICT (type_name, locale_code)
            DO UPDATE SET data = EXCLUDED.data, block_areas = EXCLUDED.block_areas, updated_at = EXCLUDED.updated_at
            """;
        cmd.Parameters.AddWithValue("t", g.TypeName);
        cmd.Parameters.AddWithValue("l", g.LocaleCode);
        cmd.Parameters.Add(new NpgsqlParameter("data", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(g.Data) });
        cmd.Parameters.Add(new NpgsqlParameter("blocks", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(g.BlockAreas) });
        cmd.Parameters.AddWithValue("updated", g.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
