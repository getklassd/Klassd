using Klassd.Abstractions.Records;

namespace Klassd.GraphQL;

// GraphQL output shapes. Data is exposed as key/value pairs (the storage model is a flat string map),
// so the schema stays stable regardless of the code-first content types.

public sealed record FieldValue(string Key, string Value);

public sealed record BlockNode(string BlockTypeName, IReadOnlyList<FieldValue> Data);

public sealed record BlockAreaNode(string Name, IReadOnlyList<BlockNode> Blocks);

public sealed record PageNode(
    string Id,
    string ContentId,
    string LocaleCode,
    string? ParentId,
    string PageTypeName,
    string Name,
    string Slug,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<FieldValue> Data,
    IReadOnlyList<BlockAreaNode> BlockAreas);

public sealed record GlobalNode(
    string TypeName,
    string LocaleCode,
    IReadOnlyList<FieldValue> Data,
    IReadOnlyList<BlockAreaNode> BlockAreas);

public sealed record LocaleNode(string Code, bool Mandatory, bool IsDefault, string? FallbackTo);

internal static class GraphMap
{
    public static PageNode ToNode(PageRecord p) => new(
        p.Id, p.ContentId, p.LocaleCode, p.ParentId, p.PageTypeName, p.Name, p.Slug,
        p.CreatedAt, p.UpdatedAt, Fields(p.Data), Areas(p.BlockAreas));

    public static GlobalNode ToNode(GlobalRecord g) => new(
        g.TypeName, g.LocaleCode, Fields(g.Data), Areas(g.BlockAreas));

    private static List<FieldValue> Fields(Dictionary<string, string> data) =>
        data.Select(kv => new FieldValue(kv.Key, kv.Value)).ToList();

    private static List<BlockAreaNode> Areas(Dictionary<string, List<BlockInstanceRecord>> areas) =>
        areas.Select(a => new BlockAreaNode(
            a.Key,
            a.Value.Select(b => new BlockNode(b.BlockTypeName, Fields(b.Data))).ToList())).ToList();
}
