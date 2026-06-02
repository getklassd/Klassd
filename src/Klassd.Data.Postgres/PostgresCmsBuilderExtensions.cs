using Klassd.Abstractions;
using Klassd.Abstractions.Media;
using Klassd.Abstractions.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        cms.Services.AddScoped<IMediaStore, MediaStore>();
        cms.Services.AddScoped<IDictionaryStore, DictionaryStore>();
        cms.Services.AddScoped<IUserStore, UserStore>();
        cms.Services.AddScoped<IPreferencesStore, PreferencesStore>();
        cms.Services.AddScoped<IUnitOfWork, PostgresUnitOfWork>();

        cms.Services.AddScoped<IStorageInitializer, PostgresSchemaInitializer>();

        return cms;
    }

    /// <summary>Reads <c>ConnectionString</c> from the given configuration section.</summary>
    public static ICmsBuilder UsePostgres(this ICmsBuilder cms, IConfiguration section) =>
        cms.UsePostgres(section["ConnectionString"] ?? string.Empty);
}
