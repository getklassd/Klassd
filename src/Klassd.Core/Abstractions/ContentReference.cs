namespace Klassd.Core.Abstractions;

/// <summary>
/// A code-first reference to another page. Declare a property of this type to create a
/// relationship field — the admin renders a page picker and stores the target page's stable
/// <see cref="Klassd.Abstractions.Records.PageRecord.ContentId"/> (locale-agnostic, so the
/// link resolves to the correct translation per request via <c>/api/pages/content/{contentId}</c>).
/// Restrict which page types may be linked with <see cref="AllowedRelationsAttribute"/>.
/// <para>
/// The persisted value is the ContentId GUID in string form — content field values are stored
/// DB-agnostically as strings (see <c>PageRecord.Data</c>). This wrapper is the typed, code-first
/// surface for that id; it converts implicitly to/from <see cref="string"/>.
/// </para>
/// </summary>
public sealed class PageReference
{
    public PageReference() { }
    public PageReference(string contentId) => ContentId = contentId;

    /// <summary>Stable cross-locale id of the linked page (its ContentId). Empty = no link.</summary>
    public string ContentId { get; set; } = string.Empty;

    public bool HasValue => !string.IsNullOrEmpty(ContentId);

    public static implicit operator PageReference(string contentId) => new(contentId);
    public static implicit operator string(PageReference? reference) => reference?.ContentId ?? string.Empty;
    public override string ToString() => ContentId;
}

/// <summary>
/// A code-first reference to a stored media item. Declare a property of this type to create a
/// media field — the admin renders the media picker and stores the selected item's id. Equivalent
/// to <c>[CmsField(FieldType = "media")]</c> on a <see cref="string"/>, but strongly typed.
/// The persisted value is the media id GUID in string form (see <see cref="PageReference"/>).
/// </summary>
public sealed class MediaReference
{
    public MediaReference() { }
    public MediaReference(string mediaId) => MediaId = mediaId;

    /// <summary>Id of the linked media item. Empty = no media selected.</summary>
    public string MediaId { get; set; } = string.Empty;

    public bool HasValue => !string.IsNullOrEmpty(MediaId);

    public static implicit operator MediaReference(string mediaId) => new(mediaId);
    public static implicit operator string(MediaReference? reference) => reference?.MediaId ?? string.Empty;
    public override string ToString() => MediaId;
}
