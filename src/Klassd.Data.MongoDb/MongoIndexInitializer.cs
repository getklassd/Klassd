using Klassd.Abstractions.Media;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>
/// Creates the adapter's indexes once on the single configured database (before
/// seeding/serving). Index creation is idempotent, so re-running is harmless.
/// </summary>
public sealed class MongoIndexInitializer(MongoContext context, IndexDefinitions indexes) : IStorageInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var db = context.Database;
        await CreatePageIndexesAsync(db, cancellationToken);
        await CreatePageVersionIndexesAsync(db, cancellationToken);
        await CreateMediaIndexesAsync(db, cancellationToken);
        await CreateGlobalIndexesAsync(db, cancellationToken);
        await CreateGeneratedIndexesAsync(db, cancellationToken);
        // No userPreferences index: UserId is mapped to _id, which Mongo already indexes
        // uniquely (a unique index on _id is rejected with "not valid for an _id index").
    }

    // Indexes generated from [Indexable] content fields + media built-in columns. JSON keys map to
    // dotted paths on the PascalCase BSON element ("Data.<key>"); media columns use the property name.
    private async Task CreateGeneratedIndexesAsync(IMongoDatabase db, CancellationToken ct)
    {
        foreach (var ix in indexes.JsonIndexes)
        {
            var collection = ix.Table switch
            {
                "pages" => MongoContext.PagesCollection,
                "globals" => MongoContext.GlobalsCollection,
                _ => null,
            };
            if (collection is null) continue;
            var path = $"{ix.JsonColumn}.{ix.Key}";   // BSON element is PascalCase "Data"
            await db.GetCollection<BsonDocument>(collection).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending(path),
                    new CreateIndexOptions { Name = $"ix_{ix.Table}_{Sanitize(ix.Key)}" }),
                cancellationToken: ct);
        }
        foreach (var ix in indexes.ColumnIndexes.Where(c => c.Table == "media"))
            await db.GetCollection<MediaRecord>(MongoContext.MediaCollection).Indexes.CreateOneAsync(
                new CreateIndexModel<MediaRecord>(
                    Builders<MediaRecord>.IndexKeys.Ascending(ix.BsonElement),
                    new CreateIndexOptions { Name = $"ix_media_{ix.SqlColumn}" }),
                cancellationToken: ct);
    }

    private static string Sanitize(string key) =>
        new(key.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

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

    private static Task CreatePageVersionIndexesAsync(IMongoDatabase db, CancellationToken ct)
    {
        var versions = db.GetCollection<PageVersionRecord>(MongoContext.PageVersionsCollection);
        var keys = Builders<PageVersionRecord>.IndexKeys;
        var models = new[]
        {
            new CreateIndexModel<PageVersionRecord>(
                keys.Ascending(x => x.PageId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_page_status" }),
            // At most one draft per page (partial unique on the draft status).
            new CreateIndexModel<PageVersionRecord>(
                keys.Ascending(x => x.PageId),
                new CreateIndexOptions<PageVersionRecord>
                {
                    Unique = true,
                    Name = "ux_draft_per_page",
                    PartialFilterExpression = Builders<PageVersionRecord>.Filter.Eq(x => x.Status, PageVersionStatus.Draft),
                }),
        };
        return versions.Indexes.CreateManyAsync(models, ct);
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
