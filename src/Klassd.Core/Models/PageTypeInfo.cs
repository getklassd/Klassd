namespace Klassd.Core.Models;

public record PageTypeInfo(
    string TypeName,
    string DisplayName,
    bool IsLocalized,
    IReadOnlyList<PageFieldInfo> Fields,
    /// <summary>null = all child types allowed; empty = no children; non-empty = specific allowed types.</summary>
    IReadOnlyList<string>? AllowedChildren,
    /// <summary>null = auto-fill from name; empty string = root slug ("").</summary>
    string? DefaultSlug,
    /// <summary>Icon name (built-in set) or literal glyph/emoji; null = default document icon.</summary>
    string? Icon = null);

public record PageFieldInfo(string Name, string DisplayName, string FieldType, bool IsLocalized = false, bool Indexable = false);
