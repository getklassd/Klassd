using Klassd.Abstractions.Records;

namespace Klassd.Abstractions.Storage;

/// <summary>
/// Persistence for translation dictionary entries (the <c>dictionary</c> table/collection). One row
/// per flat key, holding a per-locale values map. Implemented by each DB adapter (Mongo/Postgres/SQLite).
/// </summary>
public interface IDictionaryStore
{
    Task<IReadOnlyList<DictionaryEntryRecord>> GetAllAsync(CancellationToken ct = default);
    Task<DictionaryEntryRecord?> GetAsync(string key, CancellationToken ct = default);
    /// <summary>Inserts or replaces the entry for its key.</summary>
    Task UpsertAsync(DictionaryEntryRecord entry, CancellationToken ct = default);
    Task<bool> DeleteAsync(string key, CancellationToken ct = default);
}
