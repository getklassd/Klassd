using Klassd.Abstractions;
using Klassd.Abstractions.Media;
using Klassd.Abstractions.Storage;
using Klassd.Auth.Data.MongoDb;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        cms.Services.AddScoped<IPageVersionStore, PageVersionStore>();
        cms.Services.AddScoped<IMediaStore, MediaStore>();
        cms.Services.AddScoped<IDictionaryStore, DictionaryStore>();
        cms.Services.AddScoped<ISettingsStore, SettingsStore>();
        cms.Services.AddScoped<IPreferencesStore, PreferencesStore>();
        cms.Services.AddScoped<IGlobalStore, GlobalStore>();
        cms.Services.AddScoped<IUnitOfWork, MongoUnitOfWork>();

        cms.Services.TryAddSingleton(IndexDefinitions.Empty);
        cms.Services.AddScoped<IStorageInitializer, MongoIndexInitializer>();

        // Point Klassd.Auth (wired by AddKlassd) at the same database. It manages its own
        // user/session/metadata collections, so there is no collision with the CMS content collections.
        var auth = cms.Services
            .LastOrDefault(d => d.ServiceType == typeof(Klassd.Auth.Abstractions.IAuthBuilder))?
            .ImplementationInstance as Klassd.Auth.Abstractions.IAuthBuilder;
        auth?.UseMongoDb(connectionString, databaseName);

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
