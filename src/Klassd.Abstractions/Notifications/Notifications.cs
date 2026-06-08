using Klassd.Abstractions.Records;

namespace Klassd.Abstractions.Notifications;

/// <summary>Marker for an in-process notification raised synchronously by the engine.</summary>
public interface ICmsNotification;

/// <summary>
/// A "before" notification a handler can veto. Set <see cref="Cancel"/> (optionally with a
/// <see cref="CancelReason"/>) to abort the operation; the engine then does not perform the write.
/// </summary>
public interface ICancelableNotification : ICmsNotification
{
    bool Cancel { get; set; }
    string? CancelReason { get; set; }
}

/// <summary>
/// Handles a notification. Register with <c>AddNotificationHandler</c>; handlers run synchronously in
/// registration order before/after the operation. A "before" (cancelable) handler may mutate the
/// entity in-flight (e.g. stamp a field) or cancel. Throwing aborts the operation.
/// </summary>
public interface INotificationHandler<in TNotification> where TNotification : ICmsNotification
{
    Task HandleAsync(TNotification notification, CancellationToken ct = default);
}

/// <summary>Publishes notifications to their registered handlers, in order.</summary>
public interface ICmsNotifier
{
    /// <summary>
    /// Invokes every handler for <typeparamref name="TNotification"/> in registration order. Returns
    /// false if the notification is cancelable and a handler set <see cref="ICancelableNotification.Cancel"/>.
    /// </summary>
    Task<bool> PublishAsync<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : ICmsNotification;
}

/// <summary>No-op notifier (default when no notification pipeline is wired); nothing cancels.</summary>
public sealed class NullCmsNotifier : ICmsNotifier
{
    public static readonly NullCmsNotifier Instance = new();
    private NullCmsNotifier() { }
    public Task<bool> PublishAsync<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : ICmsNotification => Task.FromResult(true);
}

/// <summary>Thrown when a cancelable notification is canceled by a handler. Surfaces as a 409/error in callers.</summary>
public sealed class NotificationCanceledException(string message) : InvalidOperationException(message);

// ── Page notifications ────────────────────────────────────────────────
// "…ing" = before (cancelable, entity is mutable); "…ed" = after.

public abstract class CancelablePageNotification(PageRecord page) : ICancelableNotification
{
    public PageRecord Page { get; } = page;
    public bool Cancel { get; set; }
    public string? CancelReason { get; set; }
}

public abstract class PageNotification(PageRecord page) : ICmsNotification
{
    public PageRecord Page { get; } = page;
}

/// <summary>Before a page (or its draft) is saved — mutate <c>Page</c> or cancel.</summary>
public sealed class PageSavingNotification(PageRecord page) : CancelablePageNotification(page);
/// <summary>After a page (or its draft) was saved.</summary>
public sealed class PageSavedNotification(PageRecord page) : PageNotification(page);

/// <summary>Before a page is published — mutate <c>Page</c> or cancel.</summary>
public sealed class PagePublishingNotification(PageRecord page) : CancelablePageNotification(page);
/// <summary>After a page was published.</summary>
public sealed class PagePublishedNotification(PageRecord page) : PageNotification(page);

/// <summary>Before a page is unpublished — cancel to keep it live.</summary>
public sealed class PageUnpublishingNotification(PageRecord page) : CancelablePageNotification(page);
/// <summary>After a page was unpublished.</summary>
public sealed class PageUnpublishedNotification(PageRecord page) : PageNotification(page);

/// <summary>Before a page is deleted — cancel to keep it.</summary>
public sealed class PageDeletingNotification(PageRecord page) : CancelablePageNotification(page);
/// <summary>After a page was deleted.</summary>
public sealed class PageDeletedNotification(PageRecord page) : PageNotification(page);
