using System.Collections.Concurrent;
using Klassd.Abstractions.Media;

namespace Klassd.Examples.InMemoryMedia;

/// <summary>
/// A complete <see cref="IBlobStore"/> that keeps blob bytes in memory. One instance backs one
/// media section (its own keyspace). Useful for tests and as a template for a real backend
/// (Azure Blob, an in-house object store, …): swap the dictionary for your SDK's client and keep
/// the same three methods.
///
/// Contract notes:
///  • <c>key</c> is the section-relative object key the engine chose (e.g. "{id}.png").
///  • Content type is NOT stored here — it lives in <see cref="IMediaStore"/> — so reads return
///    only a stream.
///  • <see cref="OpenReadAsync"/> returns <c>null</c> for a missing key (not an exception).
/// </summary>
public sealed class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.Ordinal);

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        // Copy the incoming stream to bytes we own; the caller may dispose theirs after this returns.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _blobs[key] = buffer.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        // Hand back a fresh stream over a snapshot so concurrent overwrites can't corrupt the read.
        Stream? stream = _blobs.TryGetValue(key, out var bytes) ? new MemoryStream(bytes, writable: false) : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _blobs.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
