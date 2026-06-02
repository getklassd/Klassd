namespace Klassd.Core.Abstractions;

/// <summary>
/// Base for all page types. The navigation properties below are reflected as ordinary editable
/// fields (no schema change — they ride in the page's data bag) and are delivered on every page in
/// <c>/api/pages</c>, so a frontend can build its menu from the page tree.
/// </summary>
public abstract class PageBase
{
    /// <summary>Opt-in: include this page as an item in the public navigation menu.</summary>
    [CmsField(DisplayName = "Show in navigation")]
    public bool ShowInNavigation { get; set; }

    /// <summary>Optional menu label; falls back to the page name when blank. Not marked [Localized]:
    /// pages are already stored per-locale (each translation row carries its own value), and marking
    /// it would force every page type to count as localized.</summary>
    [CmsField(DisplayName = "Navigation label")]
    public string NavLabel { get; set; } = "";

    /// <summary>Sort order among siblings in the menu (ascending). Default 0.</summary>
    [CmsField(DisplayName = "Navigation order")]
    public int NavOrder { get; set; }
}
