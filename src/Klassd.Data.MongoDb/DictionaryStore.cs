using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>Single-database translation dictionary store.</summary>
public sealed class DictionaryStore(MongoContext context) : IDictionaryStore
{
    private static readonly FilterDefinitionBuilder<DictionaryEntryRecord> F = Builders<DictionaryEntryRecord>.Filter;

    public async Task<IReadOnlyList<DictionaryEntryRecord>> GetAllAsync(CancellationToken ct = default) =>
        await context.DictionaryEntries
            .Find(FilterDefinition<DictionaryEntryRecord>.Empty)
            .ToListAsync(ct);

    public async Task<DictionaryEntryRecord?> GetAsync(string key, CancellationToken ct = default) =>
        await context.DictionaryEntries
            .Find(F.Eq(x => x.Key, key))
            .FirstOrDefaultAsync(ct);

    public Task UpsertAsync(DictionaryEntryRecord entry, CancellationToken ct = default) =>
        context.DictionaryEntries.ReplaceOneAsync(
            F.Eq(x => x.Key, entry.Key), entry, new ReplaceOptions { IsUpsert = true }, ct);

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        var result = await context.DictionaryEntries.DeleteOneAsync(F.Eq(x => x.Key, key), ct);
        return result.DeletedCount > 0;
    }
}
