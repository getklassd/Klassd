namespace Klassd.Abstractions.Storage;

/// <summary>
/// Engine-computed searchable field keys, consumed by the admin search. Page/Global fields are the
/// camelCase data keys marked [Indexable] (union across types); the always-present built-ins (page
/// name/slug, media filename/displayName/altText) are searched regardless. DI singleton.
/// </summary>
public sealed class SearchableFields
{
    /// <summary>camelCase <c>data</c> keys marked [Indexable] across page types.</summary>
    public IReadOnlyList<string> PageFields { get; init; } = [];
    /// <summary>camelCase <c>data</c> keys marked [Indexable] across global types.</summary>
    public IReadOnlyList<string> GlobalFields { get; init; } = [];

    public static readonly SearchableFields Empty = new();
}
