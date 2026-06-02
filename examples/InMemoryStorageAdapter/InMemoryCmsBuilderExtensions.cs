using Klassd.Abstractions;
using Klassd.Abstractions.Media;
using Klassd.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Examples.InMemoryStorage;

/// <summary>
/// The registration seam for a storage adapter: a <c>UseXxx</c> extension on
/// <see cref="ICmsBuilder"/> that registers an implementation for every persistence interface the
/// engine resolves. Compare with <c>UseSqlite</c> / <c>UseMongoDb</c> / <c>UsePostgres</c> — the
/// only differences are the lifetimes and the backing types.
/// </summary>
public static class InMemoryCmsBuilderExtensions
{
    public static ICmsBuilder UseInMemoryStorage(this ICmsBuilder cms)
    {
        // Singletons: in-memory data must outlive request scopes (a real DB adapter typically
        // registers its stores Scoped because durable state lives in the database connection).
        cms.Services.AddSingleton<InMemoryDatabase>();

        cms.Services.AddSingleton<IPageStore, InMemoryPageStore>();
        cms.Services.AddSingleton<IMediaStore, InMemoryMediaStore>();
        cms.Services.AddSingleton<IDictionaryStore, InMemoryDictionaryStore>();
        cms.Services.AddSingleton<IUserStore, InMemoryUserStore>();
        cms.Services.AddSingleton<IPreferencesStore, InMemoryPreferencesStore>();
        cms.Services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();

        // Runs once at startup before seeding.
        cms.Services.AddSingleton<IStorageInitializer, InMemoryStorageInitializer>();

        return cms;
    }
}

// Usage in the host's Program.cs:
//
//   builder.Services
//       .AddKlassd(builder.Configuration)
//       .UseInMemoryStorage();
//
//   var app = builder.Build();
//   app.UseKlassd();
