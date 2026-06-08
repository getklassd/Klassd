namespace Klassd.Abstractions.Search;

/// <summary>
/// A unit of indexable content for the full-text search index. Storage-agnostic: the engine maps
/// pages/globals/media into these and hands them to an <see cref="ICmsSearchIndex"/>.
/// </summary>
public sealed record SearchDocument
{
    /// <summary>Stable unique index id, namespaced by kind (e.g. <c>page:{guid}</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Resource kind: <c>page</c>, <c>global</c>, <c>media</c>.</summary>
    public required string Kind { get; init; }

    public string? LocaleCode { get; init; }

    /// <summary>Primary display text (page name, media display name) — weighted highest.</summary>
    public required string Title { get; init; }

    /// <summary>Concatenated searchable text (slug + indexable field values + alt text…).</summary>
    public string? Body { get; init; }

    /// <summary>Secondary display line (e.g. the page's public path, the media filename).</summary>
    public string? Subtitle { get; init; }

    /// <summary>Admin route the hit links to.</summary>
    public string? Href { get; init; }

    /// <summary>Secondary label (page type, media section).</summary>
    public string? Tag { get; init; }
}
