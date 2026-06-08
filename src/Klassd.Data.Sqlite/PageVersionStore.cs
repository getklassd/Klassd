using System.Globalization;
using System.Text.Json;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Microsoft.Data.Sqlite;

namespace Klassd.Data.Sqlite;

/// <summary>
/// Page draft + published-history store over raw Microsoft.Data.Sqlite (the <c>page_versions</c>
/// table). One draft per page is enforced by a partial unique index; <c>data</c>/<c>block_areas</c>
/// are JSON TEXT, timestamps ISO-8601 ("o") TEXT.
/// </summary>
public sealed class PageVersionStore(SqliteContext context) : IPageVersionStore
{
    private const string Columns =
        "version_id, page_id, content_id, locale_code, status, number, name, slug, data, block_areas, publish_at, unpublish_at, created_at, created_by";
    private const string Values =
        "@vid, @pid, @cid, @loc, @status, @num, @name, @slug, @data, @blocks, @pub, @unpub, @created, @by";

    public async Task<PageVersionRecord?> GetDraftAsync(string pageId, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM page_versions WHERE page_id = @p AND status = 'draft' LIMIT 1";
        cmd.Parameters.AddWithValue("@p", pageId);
        return await ReadOneAsync(cmd, ct);
    }

    public async Task SaveDraftAsync(PageVersionRecord draft, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using (var del = conn.CreateCommand())
        {
            del.CommandText = "DELETE FROM page_versions WHERE page_id = @p AND status = 'draft'";
            del.Parameters.AddWithValue("@p", draft.PageId);
            await del.ExecuteNonQueryAsync(ct);
        }
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO page_versions ({Columns}) VALUES ({Values})";
        Bind(cmd, draft);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteDraftAsync(string pageId, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM page_versions WHERE page_id = @p AND status = 'draft'";
        cmd.Parameters.AddWithValue("@p", pageId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<PageVersionRecord>> GetHistoryAsync(string pageId, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM page_versions WHERE page_id = @p AND status <> 'draft' ORDER BY number DESC";
        cmd.Parameters.AddWithValue("@p", pageId);
        return await ReadManyAsync(cmd, ct);
    }

    public async Task<PageVersionRecord?> GetVersionAsync(string versionId, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {Columns} FROM page_versions WHERE version_id = @v";
        cmd.Parameters.AddWithValue("@v", versionId);
        return await ReadOneAsync(cmd, ct);
    }

    public async Task AppendPublishedAsync(PageVersionRecord version, int keepLast, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"INSERT INTO page_versions ({Columns}) VALUES ({Values})";
            Bind(cmd, version);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        if (keepLast > 0)
        {
            await using var prune = conn.CreateCommand();
            prune.CommandText = """
                DELETE FROM page_versions
                WHERE page_id = @p AND status <> 'draft' AND version_id NOT IN (
                    SELECT version_id FROM page_versions
                    WHERE page_id = @p AND status <> 'draft'
                    ORDER BY number DESC LIMIT @keep)
                """;
            prune.Parameters.AddWithValue("@p", version.PageId);
            prune.Parameters.AddWithValue("@keep", keepLast);
            await prune.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task DeleteForPageAsync(string pageId, CancellationToken ct = default)
    {
        await using var conn = await context.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM page_versions WHERE page_id = @p";
        cmd.Parameters.AddWithValue("@p", pageId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void Bind(SqliteCommand cmd, PageVersionRecord v)
    {
        cmd.Parameters.AddWithValue("@vid", v.VersionId);
        cmd.Parameters.AddWithValue("@pid", v.PageId);
        cmd.Parameters.AddWithValue("@cid", v.ContentId);
        cmd.Parameters.AddWithValue("@loc", v.LocaleCode);
        cmd.Parameters.AddWithValue("@status", StatusText(v.Status));
        cmd.Parameters.AddWithValue("@num", v.Number);
        cmd.Parameters.AddWithValue("@name", v.Name);
        cmd.Parameters.AddWithValue("@slug", v.Slug);
        cmd.Parameters.AddWithValue("@data", JsonSerializer.Serialize(v.Data));
        cmd.Parameters.AddWithValue("@blocks", JsonSerializer.Serialize(v.BlockAreas));
        cmd.Parameters.AddWithValue("@pub", (object?)NullableText(v.PublishAt) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@unpub", (object?)NullableText(v.UnpublishAt) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", ToText(v.CreatedAt));
        cmd.Parameters.AddWithValue("@by", (object?)v.CreatedBy ?? DBNull.Value);
    }

    private static async Task<PageVersionRecord?> ReadOneAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Map(r) : null;
    }

    private static async Task<IReadOnlyList<PageVersionRecord>> ReadManyAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<PageVersionRecord>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(Map(r));
        return list;
    }

    private static PageVersionRecord Map(SqliteDataReader r) => new()
    {
        VersionId = r.GetString(0),
        PageId = r.GetString(1),
        ContentId = r.GetString(2),
        LocaleCode = r.GetString(3),
        Status = ParseStatus(r.GetString(4)),
        Number = r.GetInt32(5),
        Name = r.GetString(6),
        Slug = r.GetString(7),
        Data = JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(8)) ?? new(),
        BlockAreas = JsonSerializer.Deserialize<Dictionary<string, List<BlockInstanceRecord>>>(r.GetString(9)) ?? new(),
        PublishAt = r.IsDBNull(10) ? null : FromText(r.GetString(10)),
        UnpublishAt = r.IsDBNull(11) ? null : FromText(r.GetString(11)),
        CreatedAt = FromText(r.GetString(12)),
        CreatedBy = r.IsDBNull(13) ? null : r.GetString(13),
    };

    private static string StatusText(PageVersionStatus s) =>
        s switch { PageVersionStatus.Draft => "draft", PageVersionStatus.Published => "published", _ => "archived" };

    private static PageVersionStatus ParseStatus(string s) =>
        s switch { "draft" => PageVersionStatus.Draft, "published" => PageVersionStatus.Published, _ => PageVersionStatus.Archived };

    private static string ToText(DateTime v) => v.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    private static string? NullableText(DateTime? v) => v is { } d ? ToText(d) : null;
    private static DateTime FromText(string v) => DateTime.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
}
