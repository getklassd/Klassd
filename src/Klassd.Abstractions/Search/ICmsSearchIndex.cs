namespace Klassd.Abstractions.Search;

/// <summary>One full-text search hit, ranked by <see cref="Score"/> (higher = more relevant).</summary>
public sealed record SearchHit(string Id, string Kind, string Title, string? Subtitle, string? Href, string? Tag, float Score);

/// <summary>
/// A maintained full-text index over <see cref="SearchDocument"/>s. Pluggable like the storage,
/// cache and blob seams — the default engine search scans content in-process; registering an
/// implementation (e.g. Lucene) replaces that with a real tokenized, ranked index.
///
/// <para>Implementations are kept current by the engine: a startup rebuild seeds the index from the
/// stores (the DB is the source of truth, so this self-heals on a fresh/empty index), and content
/// events incrementally <see cref="IndexAsync"/>/<see cref="DeleteAsync"/> thereafter.</para>
/// </summary>
public interface ICmsSearchIndex
{
    /// <summary>Adds or replaces the document with the same <see cref="SearchDocument.Id"/>.</summary>
    Task IndexAsync(SearchDocument document, CancellationToken ct = default);

    /// <summary>Adds/replaces many documents in one batch (used by the startup rebuild).</summary>
    Task IndexManyAsync(IEnumerable<SearchDocument> documents, CancellationToken ct = default);

    /// <summary>Removes a document by id (no-op if absent).</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Ranked hits for a free-text query, optionally constrained to a locale.</summary>
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, string? localeCode = null, int max = 50, CancellationToken ct = default);

    /// <summary>Total indexed document count — the startup rebuild seeds the index when this is 0.</summary>
    Task<long> CountAsync(CancellationToken ct = default);

    /// <summary>Empties the index (used before a full rebuild).</summary>
    Task ClearAsync(CancellationToken ct = default);
}
