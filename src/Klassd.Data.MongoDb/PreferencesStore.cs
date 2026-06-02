using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>Single-database per-user preferences store. One document per user (UserId is _id).</summary>
public sealed class PreferencesStore(MongoContext context) : IPreferencesStore
{
    private static readonly FilterDefinitionBuilder<UserPreferencesRecord> F = Builders<UserPreferencesRecord>.Filter;

    public async Task<UserPreferencesRecord?> GetAsync(string userId, CancellationToken ct = default) =>
        await context.UserPreferences
            .Find(F.Eq(x => x.UserId, userId))
            .FirstOrDefaultAsync(ct);

    public Task UpsertAsync(UserPreferencesRecord prefs, CancellationToken ct = default) =>
        context.UserPreferences.ReplaceOneAsync(
            F.Eq(x => x.UserId, prefs.UserId),
            prefs,
            new ReplaceOptions { IsUpsert = true },
            ct);
}
