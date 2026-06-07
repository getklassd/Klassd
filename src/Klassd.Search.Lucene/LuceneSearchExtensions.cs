using Klassd.Abstractions;
using Klassd.Abstractions.Events;
using Klassd.Abstractions.Search;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Search.Lucene;

public static class LuceneSearchExtensions
{
    /// <summary>
    /// Enables full-text search backed by an embedded Lucene.NET index. Registers the index as the
    /// engine's <see cref="ICmsSearchIndex"/> (admin search uses it for pages), a content-event
    /// listener that keeps it live, and a startup service that rebuilds it from the database when
    /// empty. Call after <c>AddKlassd()</c> and a storage adapter.
    /// </summary>
    public static ICmsBuilder UseLuceneSearch(this ICmsBuilder cms, Action<LuceneSearchOptions>? configure = null)
    {
        var options = new LuceneSearchOptions();
        configure?.Invoke(options);

        cms.Services.AddSingleton(options);
        cms.Services.AddSingleton<LuceneSearchIndex>();
        cms.Services.AddSingleton<ICmsSearchIndex>(sp => sp.GetRequiredService<LuceneSearchIndex>());
        cms.Services.AddSingleton<ICmsEventListener, LuceneIndexListener>();
        cms.Services.AddHostedService<LuceneIndexRebuilder>();
        return cms;
    }
}
