namespace Klassd.Abstractions.Events;

/// <summary>
/// The default <see cref="ICmsEventPublisher"/>: fans each published event out to every registered
/// <see cref="ICmsEventListener"/> concurrently. With no listeners registered it is a no-op. Listener
/// failures are isolated so one broken sink can neither break another nor fail the content write that
/// raised the event.
/// </summary>
public sealed class CmsEventPublisher(IEnumerable<ICmsEventListener> listeners) : ICmsEventPublisher
{
    private readonly IReadOnlyList<ICmsEventListener> _listeners = listeners as IReadOnlyList<ICmsEventListener> ?? listeners.ToList();

    public Task PublishAsync(CmsEvent evt, CancellationToken ct = default)
    {
        if (_listeners.Count == 0) return Task.CompletedTask;
        return Task.WhenAll(_listeners.Select(l => SafeNotify(l, evt, ct)));
    }

    private static async Task SafeNotify(ICmsEventListener listener, CmsEvent evt, CancellationToken ct)
    {
        try { await listener.OnEventAsync(evt, ct); }
        catch { /* listener isolation — sinks log their own failures (see ICmsEventListener) */ }
    }
}
