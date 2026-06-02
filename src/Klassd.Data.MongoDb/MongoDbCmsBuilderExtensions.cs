using Klassd.Abstractions;
using Klassd.Abstractions.Media;
using Klassd.Abstractions.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>
/// Host-facing registration for the MongoDB storage adapter.
/// Usage: <c>builder.AddKlassd().UseMongoDb(connectionString)</c>.
/// </summary>
public static class MongoDbCmsBuilderExtensions
{
    /// <summary>Registers the MongoDB adapter using an explicit connection string and target database.</summary>
    public static ICmsBuilder UseMongoDb(this ICmsBuilder cms, string connectionString, string databaseName = "klassd")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        // Class maps are process-global; register before any (de)serialization.
        MongoMappings.Register();

        var options = new MongoOptions { ConnectionString = connectionString, DatabaseName = databaseName };
        cms.Services.AddSingleton(options);
        cms.Services.AddSingleton<IMongoClient>(_ => new MongoClient(options.ConnectionString));

        cms.Services.AddScoped<MongoContext>();
        cms.Services.AddScoped<IPageStore, PageStore>();
        cms.Services.AddScoped<IMediaStore, MediaStore>();
        cms.Services.AddScoped<IDictionaryStore, DictionaryStore>();
        cms.Services.AddScoped<IUserStore, UserStore>();
        cms.Services.AddScoped<IPreferencesStore, PreferencesStore>();
        cms.Services.AddScoped<IGlobalStore, GlobalStore>();
        cms.Services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

        cms.Services.AddScoped<IStorageInitializer, MongoIndexInitializer>();

        return cms;
    }

    /// <summary>
    /// Registers the MongoDB adapter, reading <c>ConnectionString</c> and <c>DatabaseName</c>
    /// from the given configuration section (e.g. a <c>"MongoDB"</c> section).
    /// </summary>
    public static ICmsBuilder UseMongoDb(this ICmsBuilder cms, IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var connectionString = section["ConnectionString"]
            ?? throw new InvalidOperationException("MongoDB configuration is missing a 'ConnectionString' value.");
        var databaseName = section["DatabaseName"] ?? "klassd";

        return cms.UseMongoDb(connectionString, databaseName);
    }
}
