using Klassd.Abstractions;
using Klassd.Abstractions.Media;
using Klassd.Abstractions.Storage;
using Klassd.Auth.Data.Postgres;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Klassd.Data.Postgres;

/// <summary>Registers the PostgreSQL storage adapter on an <see cref="ICmsBuilder"/>.</summary>
public static class PostgresCmsBuilderExtensions
{
    public static ICmsBuilder UsePostgres(this ICmsBuilder cms, string connectionString)
    {
        var options = new PostgresOptions { ConnectionString = connectionString };
        cms.Services.AddSingleton(options);
        cms.Services.AddSingleton<INpgsqlDataSourceProvider, NpgsqlDataSourceProvider>();

        cms.Services.AddScoped<PostgresContext>();
        cms.Services.AddScoped<IPageStore, PageStore>();
        cms.Services.AddScoped<IPageVersionStore, PageVersionStore>();
        cms.Services.AddScoped<IMediaStore, MediaStore>();
        cms.Services.AddScoped<IDictionaryStore, DictionaryStore>();
        cms.Services.AddScoped<ISettingsStore, SettingsStore>();
        cms.Services.AddScoped<IPreferencesStore, PreferencesStore>();
        cms.Services.AddScoped<IGlobalStore, GlobalStore>();
        cms.Services.AddScoped<IUnitOfWork, PostgresUnitOfWork>();

        cms.Services.TryAddSingleton(IndexDefinitions.Empty);
        cms.Services.AddScoped<IStorageInitializer, PostgresSchemaInitializer>();

        // Point Klassd.Auth (wired by AddKlassd) at the same database. It manages its own
        // user/session/metadata tables, so there is no collision with the CMS content tables.
        var auth = cms.Services
            .LastOrDefault(d => d.ServiceType == typeof(Klassd.Auth.Abstractions.IAuthBuilder))?
            .ImplementationInstance as Klassd.Auth.Abstractions.IAuthBuilder;
        auth?.UsePostgres(connectionString);

        return cms;
    }

    /// <summary>Reads <c>ConnectionString</c> from the given configuration section.</summary>
    public static ICmsBuilder UsePostgres(this ICmsBuilder cms, IConfiguration section) =>
        cms.UsePostgres(section["ConnectionString"] ?? string.Empty);
}
