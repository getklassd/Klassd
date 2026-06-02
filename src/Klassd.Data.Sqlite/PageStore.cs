using System.Globalization;
using System.Text.Json;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Microsoft.Data.Sqlite;

namespace Klassd.Data.Sqlite;

/// <summary>
/// Page store over raw Microsoft.Data.Sqlite. <c>data</c> and <c>block_areas</c> are stored as
/// TEXT (JSON) via System.Text.Json; timestamps as ISO-8601 ("o") TEXT.
/// </summary>
public sealed class PageStore(SqliteContext context) : IPageStore
{
    private const string Columns =
        "id, content_id, locale_code, parent_id, page_type, name, slug, data, block_areas, created_at, updated_at";

    public async Task<IReadOnlyList<PageRecord>> GetByLocaleAsync(string localeCode, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM pages WHERE locale_code = @l";
        cmd.Parameters.AddWithValue("@l", localeCode);
        return await ReadManyAsync(cmd, ct);
    }

    public async Task<PageRecord?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM pages WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return await ReadOneAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<PageRecord>> GetByContentIdAsync(string contentId, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM pages WHERE content_id = @c";
        cmd.Parameters.AddWithValue("@c", contentId);
        return await ReadManyAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<PageRecord>> GetChildrenAsync(string parentId, string localeCode, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM pages WHERE parent_id = @p AND locale_code = @l";
        cmd.Parameters.AddWithValue("@p", parentId);
        cmd.Parameters.AddWithValue("@l", localeCode);
        return await ReadManyAsync(cmd, ct);
    }

    public async Task<PageRecord?> FindBySlugAsync(string localeCode, string slug, string? excludeId, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"SELECT {Columns} FROM pages WHERE locale_code = @l AND slug = @s AND (@x IS NULL OR id <> @x)";
        cmd.Parameters.AddWithValue("@l", localeCode);
        cmd.Parameters.AddWithValue("@s", slug);
        cmd.Parameters.AddWithValue("@x", (object?)excludeId ?? DBNull.Value);
        return await ReadOneAsync(cmd, ct);
    }

    public async Task InsertAsync(PageRecord page, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO pages ({Columns})
            VALUES (@id, @c, @l, @parent, @type, @name, @slug, @data, @blocks, @created, @updated)
            """;
        BindWrite(cmd, page);
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (IsUniqueViolation(ex))
        {
            throw SlugConflict(page.Slug, page.LocaleCode);
        }
    }

    public async Task<PageRecord?> ReplaceAsync(PageRecord page, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE pages SET
              content_id = @c, locale_code = @l, parent_id = @parent, page_type = @type,
              name = @name, slug = @slug, data = @data, block_areas = @blocks,
              created_at = @created, updated_at = @updated
            WHERE id = @id
            """;
        BindWrite(cmd, page);
        int rows;
        try
        {
            rows = await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (IsUniqueViolation(ex))
        {
            throw SlugConflict(page.Slug, page.LocaleCode);
        }

        return rows == 0 ? null : await GetByIdAsync(page.Id, ct);
    }

    public async Task UpdateSlugAsync(string id, string slug, DateTime updatedAt, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE pages SET slug = @s, updated_at = @u WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@s", slug);
        cmd.Parameters.AddWithValue("@u", ToText(updatedAt));
        try
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (SqliteException ex) when (IsUniqueViolation(ex))
        {
            // We don't know the locale here without a lookup; the engine checks slug
            // uniqueness up front, so this is a defensive translation to a 409.
            throw new InvalidOperationException($"Slug '{slug}' already exists for its locale.");
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM pages WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    private static void BindWrite(SqliteCommand cmd, PageRecord page)
    {
        cmd.Parameters.AddWithValue("@id", page.Id);
        cmd.Parameters.AddWithValue("@c", page.ContentId);
        cmd.Parameters.AddWithValue("@l", page.LocaleCode);
        cmd.Parameters.AddWithValue("@parent", (object?)page.ParentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@type", page.PageTypeName);
        cmd.Parameters.AddWithValue("@name", page.Name);
        cmd.Parameters.AddWithValue("@slug", page.Slug);
        cmd.Parameters.AddWithValue("@data", JsonSerializer.Serialize(page.Data));
        cmd.Parameters.AddWithValue("@blocks", JsonSerializer.Serialize(page.BlockAreas));
        cmd.Parameters.AddWithValue("@created", ToText(page.CreatedAt));
        cmd.Parameters.AddWithValue("@updated", ToText(page.UpdatedAt));
    }

    private static async Task<PageRecord?> ReadOneAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    private static async Task<IReadOnlyList<PageRecord>> ReadManyAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<PageRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    private static PageRecord Map(SqliteDataReader r) => new()
    {
        Id = r.GetString(0),
        ContentId = r.GetString(1),
        LocaleCode = r.GetString(2),
        ParentId = r.IsDBNull(3) ? null : r.GetString(3),
        PageTypeName = r.GetString(4),
        Name = r.GetString(5),
        Slug = r.GetString(6),
        Data = JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(7)) ?? new(),
        BlockAreas = JsonSerializer.Deserialize<Dictionary<string, List<BlockInstanceRecord>>>(r.GetString(8)) ?? new(),
        CreatedAt = FromText(r.GetString(9)),
        UpdatedAt = FromText(r.GetString(10)),
    };

    private static string ToText(DateTime value) =>
        value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    private static DateTime FromText(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();

    private static bool IsUniqueViolation(SqliteException ex) =>
        ex.SqliteErrorCode == 19 || ex.SqliteExtendedErrorCode == 2067;

    private static InvalidOperationException SlugConflict(string slug, string localeCode) =>
        new($"Slug '{slug}' already exists for locale '{localeCode}'.");
}
