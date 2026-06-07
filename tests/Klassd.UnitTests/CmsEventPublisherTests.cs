using Klassd.Abstractions.Events;
using TUnit.Core;

namespace Klassd.UnitTests;

public class CmsEventPublisherTests
{
    private sealed class Recorder : ICmsEventListener
    {
        public List<string> Seen { get; } = [];
        public Task OnEventAsync(CmsEvent evt, CancellationToken ct = default) { Seen.Add(evt.EventType); return Task.CompletedTask; }
    }

    private sealed class Thrower : ICmsEventListener
    {
        public Task OnEventAsync(CmsEvent evt, CancellationToken ct = default) => throw new InvalidOperationException("boom");
    }

    private static CmsEvent Evt() =>
        new() { EventType = CmsEventTypes.PageUpdated, ResourceKind = "page", Id = "1" };

    [Test]
    public async Task No_listeners_is_a_noop()
    {
        var publisher = new CmsEventPublisher([]);
        await publisher.PublishAsync(Evt()); // must not throw
    }

    [Test]
    public async Task Fans_out_to_every_listener()
    {
        var a = new Recorder();
        var b = new Recorder();
        var publisher = new CmsEventPublisher([a, b]);

        await publisher.PublishAsync(Evt());

        await Assert.That(a.Seen).IsEquivalentTo([CmsEventTypes.PageUpdated]);
        await Assert.That(b.Seen).IsEquivalentTo([CmsEventTypes.PageUpdated]);
    }

    [Test]
    public async Task Isolates_a_failing_listener()
    {
        var ok = new Recorder();
        var publisher = new CmsEventPublisher([new Thrower(), ok]);

        await publisher.PublishAsync(Evt()); // the thrower must not break the publish or the other listener

        await Assert.That(ok.Seen).IsEquivalentTo([CmsEventTypes.PageUpdated]);
    }
}
