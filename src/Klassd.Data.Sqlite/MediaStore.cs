using System.Globalization;
using System.Text.Json;
using Klassd.Abstractions.Media;
using Microsoft.Data.Sqlite;

namespace Klassd.Data.Sqlite;

/// <summary>
/// Media metadata store over raw Microsoft.Data.Sqlite. <c>focal_points</c> and <c>data</c> are
/// stored as TEXT (JSON) via System.Text.Json; timestamps as ISO-8601 ("o") TEXT.
/// </summary>
public sealed class MediaStore(SqliteContext context) : IMediaStore
{
    private const string Columns =
        "id, section, key, file_name, content_type, size, width, height, alt_text, focal_points, data, uploaded_at";

    public async Task<IReadOnlyList<MediaRecord>> ListAsync(string section, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM media WHERE section = @s ORDER BY uploaded_at DESC";
        cmd.Parameters.AddWithValue("@s", section);
        return await ReadManyAsync(cmd, ct);
    }

    public async Task<MediaRecord?> GetAsync(string id, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM media WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return await ReadOneAsync(cmd, ct);
    }

    public async Task InsertAsync(MediaRecord media, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO media ({Columns})
            VALUES (@id, @s, @key, @file, @ct, @size, @w, @h, @alt, @focal, @data, @uploaded)
            """;
        BindWrite(cmd, media);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<MediaRecord?> UpdateAsync(MediaRecord media, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE media SET
              section = @s, key = @key, file_name = @file, content_type = @ct, size = @size,
              width = @w, height = @h, alt_text = @alt, focal_points = @focal, data = @data,
              uploaded_at = @uploaded
            WHERE id = @id
            """;
        BindWrite(cmd, media);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows == 0 ? null : await GetAsync(media.Id, ct);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM media WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    private static void BindWrite(SqliteCommand cmd, MediaRecord media)
    {
        cmd.Parameters.AddWithValue("@id", media.Id);
        cmd.Parameters.AddWithValue("@s", media.Section);
        cmd.Parameters.AddWithValue("@key", media.Key);
        cmd.Parameters.AddWithValue("@file", media.FileName);
        cmd.Parameters.AddWithValue("@ct", media.ContentType);
        cmd.Parameters.AddWithValue("@size", media.Size);
        cmd.Parameters.AddWithValue("@w", (object?)media.Width ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@h", (object?)media.Height ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@alt", (object?)media.AltText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@focal", JsonSerializer.Serialize(media.FocalPoints));
        cmd.Parameters.AddWithValue("@data", JsonSerializer.Serialize(media.Data));
        cmd.Parameters.AddWithValue("@uploaded", ToText(media.UploadedAt));
    }

    private static async Task<MediaRecord?> ReadOneAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    private static async Task<IReadOnlyList<MediaRecord>> ReadManyAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<MediaRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    private static MediaRecord Map(SqliteDataReader r) => new()
    {
        Id = r.GetString(0),
        Section = r.GetString(1),
        Key = r.GetString(2),
        FileName = r.GetString(3),
        ContentType = r.GetString(4),
        Size = r.GetInt64(5),
        Width = r.IsDBNull(6) ? null : r.GetInt32(6),
        Height = r.IsDBNull(7) ? null : r.GetInt32(7),
        AltText = r.IsDBNull(8) ? null : r.GetString(8),
        FocalPoints = JsonSerializer.Deserialize<List<MediaFocalPoint>>(r.GetString(9)) ?? [],
        Data = JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(10)) ?? new(),
        UploadedAt = FromText(r.GetString(11)),
    };

    private static string ToText(DateTime value) =>
        value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    private static DateTime FromText(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
}
