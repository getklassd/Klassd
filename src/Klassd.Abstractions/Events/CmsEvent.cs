namespace Klassd.Abstractions.Events;

/// <summary>
/// A content change worth notifying external subscribers about (webhooks, cache busters,
/// search indexers, …). Intentionally flat and serialization-friendly — it is the wire
/// payload delivered to webhook endpoints.
/// </summary>
public sealed record CmsEvent
{
    /// <summary>Dotted event name, e.g. <c>page.updated</c>. See <see cref="CmsEventTypes"/>.</summary>
    public required string EventType { get; init; }

    /// <summary>When the change happened (UTC).</summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>The kind of resource that changed, e.g. <c>page</c>.</summary>
    public required string ResourceKind { get; init; }

    /// <summary>The changed record's storage id.</summary>
    public required string Id { get; init; }

    /// <summary>The locale-independent content id (groups translations of one page).</summary>
    public string? ContentId { get; init; }

    public string? LocaleCode { get; init; }
    public string? Slug { get; init; }

    /// <summary>The content type name (page type / global type).</summary>
    public string? TypeName { get; init; }
}
