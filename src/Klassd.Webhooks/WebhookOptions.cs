namespace Klassd.Webhooks;

/// <summary>A single webhook endpoint and the events it wants.</summary>
public sealed class WebhookSubscription
{
    /// <summary>The endpoint that receives the POST.</summary>
    public required string Url { get; init; }

    /// <summary>Shared secret; when set, requests carry an <c>X-Klassd-Signature: sha256=…</c> HMAC of the body.</summary>
    public string? Secret { get; init; }

    /// <summary>Event types to deliver (see <c>CmsEventTypes</c>). Empty = every event.</summary>
    public string[] Events { get; init; } = [];

    internal bool Matches(string eventType) => Events.Length == 0 || Array.IndexOf(Events, eventType) >= 0;
}

/// <summary>Configuration for <see cref="WebhookDispatcher"/>.</summary>
public sealed class WebhookOptions
{
    internal const string HttpClientName = "klassd-webhooks";

    /// <summary>The endpoints to notify.</summary>
    public List<WebhookSubscription> Subscriptions { get; } = [];

    /// <summary>Per-request timeout for delivery attempts.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
