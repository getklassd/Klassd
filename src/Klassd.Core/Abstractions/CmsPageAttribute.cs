namespace Klassd.Core.Abstractions;

/// <summary>Metadata for a page type — controls CMS behaviour at the type level.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CmsPageAttribute : Attribute
{
    /// <summary>
    /// Default slug inserted when this page type is selected in the editor.
    /// Null = auto-fill from name. Empty string = root ("/").
    /// </summary>
    public string? DefaultSlug { get; set; }

    /// <summary>
    /// Icon shown next to this page type in the admin (tree, pickers). Either a name from the
    /// built-in line-icon set (e.g. "house", "folder", "image", "file") or any literal glyph /
    /// emoji (e.g. "🏠"), which is rendered as-is. Null falls back to a generic document icon.
    /// </summary>
    public string? Icon { get; set; }
}
