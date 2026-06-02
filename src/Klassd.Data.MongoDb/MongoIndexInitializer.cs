using Klassd.Abstractions.Media;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>
/// Creates the adapter's indexes once on the single configured database (before
/// seeding/serving). Index creation is idempotent, so re-running is harmless.
/// </summary>
public sealed class MongoIndexInitializer(MongoContext context) : IStorageInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var db = context.Database;
        await CreatePageIndexesAsync(db, cancellationToken);
        await CreateUserIndexesAsync(db, cancellationToken);
        await CreateMediaIndexesAsync(db, cancellationToken);
        await CreateGlobalIndexesAsync(db, cancellationToken);
        // No userPreferences index: UserId is mapped to _id, which Mongo already indexes
        // uniquely (a unique index on _id is rejected with "not valid for an _id index").
    }

    private static Task CreateGlobalIndexesAsync(IMongoDatabase db, CancellationToken ct)
    {
        var globals = db.GetCollection<GlobalRecord>(MongoContext.GlobalsCollection);
        var keys = Builders<GlobalRecord>.IndexKeys;
        return globals.Indexes.CreateOneAsync(
            new CreateIndexModel<GlobalRecord>(
                keys.Ascending(x => x.TypeName).Ascending(x => x.LocaleCode),
                new CreateIndexOptions { Unique = true, Name = "ux_type_locale" }),
            cancellationToken: ct);
    }

    private static Task CreatePageIndexesAsync(IMongoDatabase db, CancellationToken ct)
    {
        var pages = db.GetCollection<PageRecord>(MongoContext.PagesCollection);
        var keys = Builders<PageRecord>.IndexKeys;

        var models = new[]
        {
            new CreateIndexModel<PageRecord>(
                keys.Ascending(x => x.LocaleCode).Ascending(x => x.Slug),
                new CreateIndexOptions { Unique = true, Name = "ux_locale_slug" }),
            new CreateIndexModel<PageRecord>(
                keys.Ascending(x => x.ContentId),
                new CreateIndexOptions { Name = "ix_content" }),
            new CreateIndexModel<PageRecord>(
                keys.Ascending(x => x.ParentId).Ascending(x => x.LocaleCode),
                new CreateIndexOptions { Name = "ix_parent_locale" }),
        };

        return pages.Indexes.CreateManyAsync(models, ct);
    }

    private static Task CreateUserIndexesAsync(IMongoDatabase db, CancellationToken ct)
    {
        var users = db.GetCollection<UserRecord>(MongoContext.UsersCollection);
        var keys = Builders<UserRecord>.IndexKeys;

        var models = new[]
        {
            new CreateIndexModel<UserRecord>(
                keys.Ascending(x => x.Username),
                new CreateIndexOptions { Unique = true, Name = "ux_username" }),
            // Non-unique: many local users share provider="local"/null external_id.
            new CreateIndexModel<UserRecord>(
                keys.Ascending(x => x.Provider).Ascending(x => x.ExternalId),
                new CreateIndexOptions { Name = "ix_provider_external" }),
            // Non-unique: email may repeat or be null.
            new CreateIndexModel<UserRecord>(
                keys.Ascending(x => x.Email),
                new CreateIndexOptions { Name = "ix_email" }),
        };

        return users.Indexes.CreateManyAsync(models, ct);
    }

    private static Task CreateMediaIndexesAsync(IMongoDatabase db, CancellationToken ct)
    {
        var media = db.GetCollection<MediaRecord>(MongoContext.MediaCollection);
        var keys = Builders<MediaRecord>.IndexKeys;
        return media.Indexes.CreateOneAsync(
            new CreateIndexModel<MediaRecord>(
                keys.Ascending(x => x.Section),
                new CreateIndexOptions { Name = "ix_section" }),
            cancellationToken: ct);
    }
}
