namespace Klassd.Abstractions.Events;

/// <summary>Well-known <see cref="CmsEvent.EventType"/> values raised by the engine.</summary>
public static class CmsEventTypes
{
    public const string PageCreated = "page.created";
    public const string PageUpdated = "page.updated";
    public const string PageDeleted = "page.deleted";
}
