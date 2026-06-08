using Klassd.Abstractions;
using Klassd.Abstractions.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Backoffice;

/// <summary>
/// Default <see cref="ICmsNotifier"/>: invokes the registered <see cref="INotificationHandler{T}"/>s
/// for a notification synchronously, in registration order. A handler that throws aborts the operation;
/// a cancelable notification a handler marks <c>Cancel</c> stops it cleanly.
/// </summary>
public sealed class CmsNotifier(IServiceProvider services) : ICmsNotifier
{
    public async Task<bool> PublishAsync<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : ICmsNotification
    {
        foreach (var handler in services.GetServices<INotificationHandler<TNotification>>())
            await handler.HandleAsync(notification, ct);
        return notification is not ICancelableNotification c || !c.Cancel;
    }
}

public static class NotificationExtensions
{
    /// <summary>
    /// Registers an in-process notification handler. Handlers run synchronously in registration
    /// order; "…ing" (cancelable) handlers may mutate the entity or cancel the operation.
    /// </summary>
    public static ICmsBuilder AddNotificationHandler<TNotification, THandler>(this ICmsBuilder cms)
        where TNotification : ICmsNotification
        where THandler : class, INotificationHandler<TNotification>
    {
        cms.Services.AddScoped<INotificationHandler<TNotification>, THandler>();
        return cms;
    }
}
