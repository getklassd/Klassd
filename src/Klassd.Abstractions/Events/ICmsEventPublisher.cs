namespace Klassd.Abstractions.Events;

/// <summary>
/// Publishes <see cref="CmsEvent"/>s raised by the engine. The default registration is
/// <see cref="NullCmsEventPublisher"/> (no-op); the Webhooks package replaces it to deliver
/// events to subscribed HTTP endpoints. Publishing must never throw into the caller — a failed
/// delivery should not fail the content write.
/// </summary>
public interface ICmsEventPublisher
{
    Task PublishAsync(CmsEvent evt, CancellationToken ct = default);
}

/// <summary>No-op publisher used when no event sink (e.g. webhooks) is configured.</summary>
public sealed class NullCmsEventPublisher : ICmsEventPublisher
{
    public static readonly NullCmsEventPublisher Instance = new();
    private NullCmsEventPublisher() { }
    public Task PublishAsync(CmsEvent evt, CancellationToken ct = default) => Task.CompletedTask;
}
