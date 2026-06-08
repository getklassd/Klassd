namespace Klassd.Abstractions.Events;

/// <summary>Well-known <see cref="CmsEvent.EventType"/> values raised by the engine.</summary>
public static class CmsEventTypes
{
    public const string PageCreated = "page.created";
    public const string PageUpdated = "page.updated";
    public const string PageDeleted = "page.deleted";

    /// <summary>A page's draft was promoted to the live/published snapshot.</summary>
    public const string PagePublished = "page.published";

    /// <summary>A page was taken offline (no longer delivered), history retained.</summary>
    public const string PageUnpublished = "page.unpublished";
}
