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
}
