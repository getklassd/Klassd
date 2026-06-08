using Klassd.Abstractions.Storage;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>Key/value system settings store (<c>settings</c> collection). The key is the document <c>_id</c>.</summary>
public sealed class SettingsStore(MongoContext context) : ISettingsStore
{
    private static readonly FilterDefinitionBuilder<SettingDocument> F = Builders<SettingDocument>.Filter;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var doc = await context.Settings.Find(F.Eq(x => x.Key, key)).FirstOrDefaultAsync(ct);
        return doc?.Value;
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default) =>
        context.Settings.ReplaceOneAsync(
            F.Eq(x => x.Key, key),
            new SettingDocument { Key = key, Value = value },
            new ReplaceOptions { IsUpsert = true }, ct);

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        var result = await context.Settings.DeleteOneAsync(F.Eq(x => x.Key, key), ct);
        return result.DeletedCount > 0;
    }
}

/// <summary>Adapter-local document for the settings collection (the key is stored as <c>_id</c>).</summary>
public sealed class SettingDocument
{
    [BsonId] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
