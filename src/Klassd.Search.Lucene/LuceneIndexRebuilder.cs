using Klassd.Abstractions.Search;
using Klassd.Abstractions.Storage;
using Klassd.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Klassd.Search.Lucene;

/// <summary>
/// Seeds the Lucene index from the database at startup — the DB is the source of truth, so an
/// empty/fresh index (new pod, in-memory index, wiped volume) self-heals on boot. Runs in the
/// background so a large rebuild never blocks the app from serving. Rebuilds only when the index is
/// empty unless <see cref="LuceneSearchOptions.RebuildOnStartup"/> forces it. Indexes pages across
/// every locale; live updates thereafter flow through <see cref="LuceneIndexListener"/>.
/// </summary>
public sealed class LuceneIndexRebuilder(
    ICmsSearchIndex index,
    IServiceScopeFactory scopes,
    LocaleRegistry locales,
    SearchableFields searchable,
    LuceneSearchOptions options,
    ILogger<LuceneIndexRebuilder> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            if (!options.RebuildOnStartup && await index.CountAsync(ct) > 0)
                return; // already populated (e.g. a persistent on-disk index survived restart)

            if (options.RebuildOnStartup)
                await index.ClearAsync(ct);

            using var scope = scopes.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IPageStore>();

            var docs = new List<SearchDocument>();
            foreach (var locale in locales.All)
            {
                ct.ThrowIfCancellationRequested();
                foreach (var page in await store.GetByLocaleAsync(locale.Code, ct))
                    docs.Add(LucenePageMapper.ToDocument(page, searchable));
            }

            if (docs.Count > 0)
                await index.IndexManyAsync(docs, ct);

            logger.LogInformation("Lucene search index rebuilt from database: {Count} pages", docs.Count);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lucene search index rebuild failed; search may be incomplete until the next write or restart");
        }
    }
}
