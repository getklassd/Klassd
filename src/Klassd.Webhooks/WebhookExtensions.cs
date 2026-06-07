using Klassd.Abstractions;
using Klassd.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Webhooks;

public static class WebhookExtensions
{
    /// <summary>
    /// Delivers engine content-change events to subscribed HTTP endpoints. Replaces the default
    /// no-op <see cref="ICmsEventPublisher"/>. Call after <c>AddKlassd()</c>.
    /// </summary>
    /// <example>
    /// builder.UseWebhooks(o => o.Subscriptions.Add(new WebhookSubscription
    /// {
    ///     Url = "https://example.com/hooks/klassd",
    ///     Secret = "…",
    ///     Events = [CmsEventTypes.PageCreated, CmsEventTypes.PageUpdated],
    /// }));
    /// </example>
    public static ICmsBuilder UseWebhooks(this ICmsBuilder cms, Action<WebhookOptions> configure)
    {
        var options = new WebhookOptions();
        configure(options);

        cms.Services.AddSingleton(options);
        cms.Services.AddHttpClient(WebhookOptions.HttpClientName, c => c.Timeout = options.Timeout);
        cms.Services.AddSingleton<ICmsEventPublisher, WebhookDispatcher>(); // overrides the no-op default
        return cms;
    }
}
