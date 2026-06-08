using Klassd.Abstractions.Media;
using Klassd.Abstractions.Records;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>
/// Exposes the single configured <see cref="IMongoDatabase"/> and its typed collections.
/// Scoped: one per request, sharing the singleton <see cref="IMongoClient"/>.
/// </summary>
public sealed class MongoContext(IMongoClient client, MongoOptions options)
{
    public const string PagesCollection = "pages";
    public const string PageVersionsCollection = "pageVersions";
    public const string UsersCollection = "users";
    public const string UserPreferencesCollection = "userPreferences";
    public const string MediaCollection = "media";
    public const string DictionaryCollection = "dictionary";
    public const string GlobalsCollection = "globals";

    public IMongoDatabase Database { get; } = client.GetDatabase(options.DatabaseName);

    public IMongoCollection<PageRecord> Pages => Database.GetCollection<PageRecord>(PagesCollection);
    public IMongoCollection<PageVersionRecord> PageVersions => Database.GetCollection<PageVersionRecord>(PageVersionsCollection);
    public IMongoCollection<UserRecord> Users => Database.GetCollection<UserRecord>(UsersCollection);
    public IMongoCollection<UserPreferencesRecord> UserPreferences =>
        Database.GetCollection<UserPreferencesRecord>(UserPreferencesCollection);
    public IMongoCollection<MediaRecord> Media => Database.GetCollection<MediaRecord>(MediaCollection);
    public IMongoCollection<DictionaryEntryRecord> DictionaryEntries =>
        Database.GetCollection<DictionaryEntryRecord>(DictionaryCollection);
    public IMongoCollection<GlobalRecord> Globals => Database.GetCollection<GlobalRecord>(GlobalsCollection);
}
