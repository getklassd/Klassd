using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;

namespace Klassd.Examples.InMemoryStorage;

/// <summary><see cref="IDictionaryStore"/> — translation dictionary entries, one per flat key.</summary>
public sealed class InMemoryDictionaryStore(InMemoryDatabase db) : IDictionaryStore
{
    public Task<IReadOnlyList<DictionaryEntryRecord>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DictionaryEntryRecord>>(db.Dictionary.Values.Select(e => e.Clone()).ToList());

    public Task<DictionaryEntryRecord?> GetAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(db.Dictionary.TryGetValue(key, out var e) ? e.Clone() : null);

    public Task UpsertAsync(DictionaryEntryRecord entry, CancellationToken ct = default)
    {
        db.Dictionary[entry.Key] = entry.Clone();
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(db.Dictionary.TryRemove(key, out _));
}
