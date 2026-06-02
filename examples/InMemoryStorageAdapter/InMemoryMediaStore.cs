using Klassd.Abstractions.Media;

namespace Klassd.Examples.InMemoryStorage;

/// <summary>
/// <see cref="IMediaStore"/> — media <i>metadata</i> only. The bytes live in the section's
/// <see cref="IBlobStore"/>; this just tracks the record (filename, content type, focal points, …).
/// </summary>
public sealed class InMemoryMediaStore(InMemoryDatabase db) : IMediaStore
{
    public Task<IReadOnlyList<MediaRecord>> ListAsync(string section, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MediaRecord>>(
            db.Media.Values.Where(m => m.Section == section).Select(m => m.Clone()).ToList());

    public Task<MediaRecord?> GetAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(db.Media.TryGetValue(id, out var m) ? m.Clone() : null);

    public Task InsertAsync(MediaRecord media, CancellationToken ct = default)
    {
        db.Media[media.Id] = media.Clone();
        return Task.CompletedTask;
    }

    public Task<MediaRecord?> UpdateAsync(MediaRecord media, CancellationToken ct = default)
    {
        if (!db.Media.ContainsKey(media.Id))
            return Task.FromResult<MediaRecord?>(null);

        var stored = media.Clone();
        db.Media[media.Id] = stored;
        return Task.FromResult<MediaRecord?>(stored.Clone());
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(db.Media.TryRemove(id, out _));
}
