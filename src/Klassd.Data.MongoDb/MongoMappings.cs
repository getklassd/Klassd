using Klassd.Abstractions.Media;
using Klassd.Abstractions.Records;
using MongoDB.Bson.Serialization;

namespace Klassd.Data.MongoDb;

/// <summary>
/// Registers <see cref="BsonClassMap"/>s so the shared DB-agnostic records stay
/// free of Bson attributes. Ids are GUID strings end-to-end, so <c>_id</c> is
/// mapped as a plain string (no ObjectId coupling). Idempotent — safe to call
/// more than once (driver class-map registration is process-global).
/// </summary>
public static class MongoMappings
{
    private static readonly object Gate = new();

    public static void Register()
    {
        lock (Gate)
        {
            if (!BsonClassMap.IsClassMapRegistered(typeof(PageRecord)))
            {
                BsonClassMap.RegisterClassMap<PageRecord>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(x => x.Id); // GUID string stored as _id
                    cm.SetIgnoreExtraElements(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(BlockInstanceRecord)))
            {
                BsonClassMap.RegisterClassMap<BlockInstanceRecord>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIgnoreExtraElements(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(UserRecord)))
            {
                BsonClassMap.RegisterClassMap<UserRecord>(cm =>
                {
                    // AutoMap covers all public properties, including Email, Provider,
                    // ExternalId and Disabled (nullable strings serialize fine in BSON).
                    cm.AutoMap();
                    cm.MapIdMember(x => x.Id); // GUID string stored as _id
                    cm.SetIgnoreExtraElements(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(UserPreferencesRecord)))
            {
                BsonClassMap.RegisterClassMap<UserPreferencesRecord>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(x => x.UserId); // one prefs doc per user; UserId is _id
                    cm.SetIgnoreExtraElements(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(MediaRecord)))
            {
                BsonClassMap.RegisterClassMap<MediaRecord>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(x => x.Id); // GUID string stored as _id
                    cm.SetIgnoreExtraElements(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(GlobalRecord)))
            {
                BsonClassMap.RegisterClassMap<GlobalRecord>(cm =>
                {
                    // Composite key (TypeName, LocaleCode) — no single _id member; the driver
                    // generates _id and a unique index enforces the key (see MongoIndexInitializer).
                    cm.AutoMap();
                    cm.SetIgnoreExtraElements(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(DictionaryEntryRecord)))
            {
                BsonClassMap.RegisterClassMap<DictionaryEntryRecord>(cm =>
                {
                    cm.AutoMap();
                    cm.MapIdMember(x => x.Key); // flat key stored as _id (inherently unique)
                    cm.SetIgnoreExtraElements(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(MediaFocalPoint)))
            {
                BsonClassMap.RegisterClassMap<MediaFocalPoint>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIgnoreExtraElements(true);
                });
            }
        }
    }
}
