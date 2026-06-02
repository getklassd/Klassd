namespace Klassd.Abstractions.Records;

/// <summary>
/// DB-agnostic representation of a "global": a single editable singleton instance of a
/// [CmsGlobal] type, stored once per (type, locale). Not a node in the page tree.
/// </summary>
public sealed class GlobalRecord
{
    /// <summary>CLR type name of the global, e.g. "SiteHeader". Doubles as the delivery route segment.</summary>
    public string TypeName { get; set; } = string.Empty;
    public string LocaleCode { get; set; } = string.Empty;

    public Dictionary<string, string> Data { get; set; } = new();

    /// <summary>Named block areas keyed by camelCase property name (e.g. "columns").</summary>
    public Dictionary<string, List<BlockInstanceRecord>> BlockAreas { get; set; } = new();

    public DateTime UpdatedAt { get; set; }
}
