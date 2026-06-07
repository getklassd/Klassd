using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Klassd.Abstractions.Events;
using Microsoft.Extensions.Logging;

namespace Klassd.Webhooks;

/// <summary>
/// Delivers <see cref="CmsEvent"/>s to subscribed HTTP endpoints. Each matching subscription
/// gets a POST with the JSON event body, an <c>X-Klassd-Event</c> header, and (when a secret is
/// configured) an <c>X-Klassd-Signature: sha256=…</c> HMAC of the raw body so receivers can verify
/// authenticity. Delivery is best-effort: failures are logged, never thrown back into the content
/// write. Sends run concurrently and the call awaits them, bounded by the per-request timeout.
/// </summary>
public sealed class WebhookDispatcher(
    IHttpClientFactory httpClientFactory,
    WebhookOptions options,
    ILogger<WebhookDispatcher> logger) : ICmsEventPublisher
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(CmsEvent evt, CancellationToken ct = default)
    {
        var targets = options.Subscriptions.Where(s => s.Matches(evt.EventType)).ToList();
        if (targets.Count == 0) return;

        var body = JsonSerializer.SerializeToUtf8Bytes(evt, Json);
        await Task.WhenAll(targets.Select(s => SendAsync(s, evt.EventType, body, ct)));
    }

    private async Task SendAsync(WebhookSubscription sub, string eventType, byte[] body, CancellationToken ct)
    {
        try
        {
            using var content = new ByteArrayContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, sub.Url) { Content = content };
            request.Headers.TryAddWithoutValidation("X-Klassd-Event", eventType);
            if (!string.IsNullOrEmpty(sub.Secret))
                request.Headers.TryAddWithoutValidation("X-Klassd-Signature", "sha256=" + Sign(body, sub.Secret));

            var client = httpClientFactory.CreateClient(WebhookOptions.HttpClientName);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Webhook to {Url} returned {Status} for {Event}", sub.Url, (int)response.StatusCode, eventType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook delivery to {Url} failed for {Event}", sub.Url, eventType);
        }
    }

    /// <summary>The hex HMAC-SHA256 of <paramref name="body"/> under <paramref name="secret"/> (no <c>sha256=</c> prefix). Receivers recompute this to verify.</summary>
    public static string Sign(byte[] body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexStringLower(hmac.ComputeHash(body));
    }
}
