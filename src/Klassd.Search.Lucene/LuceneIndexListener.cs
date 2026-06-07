using Klassd.Abstractions.Events;
using Klassd.Abstractions.Search;
using Klassd.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Klassd.Search.Lucene;

/// <summary>
/// Keeps the Lucene index live: maps page content events to index upserts/deletes. Loads the page
/// in a fresh DI scope (IPageStore is scoped). Failures are logged, never rethrown — search staleness
/// must not fail a content write (the publisher also isolates listeners).
/// </summary>
public sealed class LuceneIndexListener(
    ICmsSearchIndex index,
    IServiceScopeFactory scopes,
    SearchableFields searchable,
    ILogger<LuceneIndexListener> logger) : ICmsEventListener
{
    public async Task OnEventAsync(CmsEvent evt, CancellationToken ct = default)
    {
        if (evt.ResourceKind != "page") return; // only pages are indexed today

        try
        {
            if (evt.EventType == CmsEventTypes.PageDeleted)
            {
                await index.DeleteAsync(LucenePageMapper.DocId(evt.Id), ct);
                return;
            }

            using var scope = scopes.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IPageStore>();
            var page = await store.GetByIdAsync(evt.Id, ct);
            if (page is not null)
                await index.IndexAsync(LucenePageMapper.ToDocument(page, searchable), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lucene index update failed for {Event} {Id}", evt.EventType, evt.Id);
        }
    }
}
