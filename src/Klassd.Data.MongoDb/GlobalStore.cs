using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>Singleton-content store. One document per (type, locale); composite key via a unique index.</summary>
public sealed class GlobalStore(MongoContext context) : IGlobalStore
{
    private static readonly FilterDefinitionBuilder<GlobalRecord> F = Builders<GlobalRecord>.Filter;

    private static FilterDefinition<GlobalRecord> Key(string typeName, string localeCode) =>
        F.And(F.Eq(x => x.TypeName, typeName), F.Eq(x => x.LocaleCode, localeCode));

    public async Task<GlobalRecord?> GetAsync(string typeName, string localeCode, CancellationToken ct = default) =>
        await context.Globals.Find(Key(typeName, localeCode)).FirstOrDefaultAsync(ct);

    public Task UpsertAsync(GlobalRecord g, CancellationToken ct = default) =>
        context.Globals.ReplaceOneAsync(
            Key(g.TypeName, g.LocaleCode), g, new ReplaceOptions { IsUpsert = true }, ct);
}
