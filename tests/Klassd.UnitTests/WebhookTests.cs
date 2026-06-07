using System.Net;
using System.Text;
using Klassd.Abstractions.Events;
using Klassd.Webhooks;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;

namespace Klassd.UnitTests;

public class WebhookDispatcherTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<(HttpRequestMessage Req, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Calls.Add((request, body));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static WebhookDispatcher New(CapturingHandler handler, params WebhookSubscription[] subs)
    {
        var opts = new WebhookOptions();
        opts.Subscriptions.AddRange(subs);
        return new WebhookDispatcher(new StubFactory(handler), opts, NullLogger<WebhookDispatcher>.Instance);
    }

    private static CmsEvent Evt(string type = CmsEventTypes.PageUpdated) =>
        new() { EventType = type, ResourceKind = "page", Id = "1", ContentId = "c1", LocaleCode = "en", Slug = "home", TypeName = "ContentPage" };

    [Test]
    public async Task Delivers_to_matching_subscription_with_event_header()
    {
        var handler = new CapturingHandler();
        var dispatcher = New(handler, new WebhookSubscription { Url = "https://example.com/hook" });

        await dispatcher.PublishAsync(Evt());

        await Assert.That(handler.Calls.Count).IsEqualTo(1);
        await Assert.That(handler.Calls[0].Req.Headers.GetValues("X-Klassd-Event").First()).IsEqualTo("page.updated");
    }

    [Test]
    public async Task Skips_subscription_not_subscribed_to_event()
    {
        var handler = new CapturingHandler();
        var dispatcher = New(handler, new WebhookSubscription { Url = "https://example.com/hook", Events = [CmsEventTypes.PageDeleted] });

        await dispatcher.PublishAsync(Evt(CmsEventTypes.PageCreated));

        await Assert.That(handler.Calls.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Signs_body_when_secret_set()
    {
        var handler = new CapturingHandler();
        var dispatcher = New(handler, new WebhookSubscription { Url = "https://example.com/hook", Secret = "s3cr3t" });

        await dispatcher.PublishAsync(Evt());

        var call = handler.Calls[0];
        var sig = call.Req.Headers.GetValues("X-Klassd-Signature").First();
        var expected = "sha256=" + WebhookDispatcher.Sign(Encoding.UTF8.GetBytes(call.Body), "s3cr3t");
        await Assert.That(sig).IsEqualTo(expected);
    }

    [Test]
    public async Task No_signature_header_without_secret()
    {
        var handler = new CapturingHandler();
        var dispatcher = New(handler, new WebhookSubscription { Url = "https://example.com/hook" });

        await dispatcher.PublishAsync(Evt());

        await Assert.That(handler.Calls[0].Req.Headers.Contains("X-Klassd-Signature")).IsFalse();
    }
}
