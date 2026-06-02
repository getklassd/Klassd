namespace Klassd.Abstractions.Media;

/// <summary>
/// Media metadata persistence (the <c>media</c> table/collection). Implemented by each
/// DB adapter (Mongo/Postgres/SQLite) alongside <c>IPageStore</c>.
/// </summary>
public interface IMediaStore
{
    Task<IReadOnlyList<MediaRecord>> ListAsync(string section, CancellationToken ct = default);
    Task<MediaRecord?> GetAsync(string id, CancellationToken ct = default);
    Task InsertAsync(MediaRecord media, CancellationToken ct = default);
    /// <summary>Replaces a record's editable metadata (alt text, focal points, data). Returns updated record or null if missing.</summary>
    Task<MediaRecord?> UpdateAsync(MediaRecord media, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
