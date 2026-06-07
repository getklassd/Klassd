namespace Klassd.Abstractions.Events;

/// <summary>
/// Reacts to a <see cref="CmsEvent"/>. Register any number of listeners (webhooks, a search
/// indexer, cache busters, …); the <see cref="CmsEventPublisher"/> fans each published event out
/// to all of them. A listener must not throw — the publisher isolates failures, but a listener
/// should handle and log its own errors so one slow/broken sink can't affect the others.
/// </summary>
public interface ICmsEventListener
{
    Task OnEventAsync(CmsEvent evt, CancellationToken ct = default);
}
