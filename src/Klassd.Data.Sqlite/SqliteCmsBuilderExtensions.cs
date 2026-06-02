using Klassd.Abstractions;
using Klassd.Abstractions.Media;
using Klassd.Abstractions.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Data.Sqlite;

/// <summary>Registers the SQLite storage adapter on an <see cref="ICmsBuilder"/>.</summary>
public static class SqliteCmsBuilderExtensions
{
    public static ICmsBuilder UseSqlite(this ICmsBuilder cms, string connectionString)
    {
        var options = new SqliteOptions { ConnectionString = connectionString };
        cms.Services.AddSingleton(options);

        cms.Services.AddScoped<SqliteContext>();
        cms.Services.AddScoped<IPageStore, PageStore>();
        cms.Services.AddScoped<IMediaStore, MediaStore>();
        cms.Services.AddScoped<IDictionaryStore, DictionaryStore>();
        cms.Services.AddScoped<IUserStore, UserStore>();
        cms.Services.AddScoped<IPreferencesStore, PreferencesStore>();
        cms.Services.AddScoped<IUnitOfWork, SqliteUnitOfWork>();

        cms.Services.AddScoped<IStorageInitializer, SqliteSchemaInitializer>();

        return cms;
    }

    /// <summary>Reads <c>ConnectionString</c> from the given configuration section.</summary>
    public static ICmsBuilder UseSqlite(this ICmsBuilder cms, IConfiguration section) =>
        cms.UseSqlite(section["ConnectionString"] ?? string.Empty);
}
