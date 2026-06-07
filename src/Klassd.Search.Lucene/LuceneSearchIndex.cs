using Klassd.Abstractions.Search;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Directory = Lucene.Net.Store.Directory;

namespace Klassd.Search.Lucene;

/// <summary>
/// <see cref="ICmsSearchIndex"/> backed by an embedded Lucene.NET index (on-disk or in-memory).
/// Storage-agnostic — it indexes <see cref="SearchDocument"/>s regardless of the DB backend. A
/// single shared <see cref="IndexWriter"/> serializes writes; reads use near-real-time readers.
/// </summary>
public sealed class LuceneSearchIndex : ICmsSearchIndex, IDisposable
{
    private const LuceneVersion V = LuceneVersion.LUCENE_48;
    private static readonly string[] QueryFields = ["title", "body"];

    private readonly Directory _dir;
    private readonly Analyzer _analyzer;
    private readonly IndexWriter _writer;
    private readonly object _gate = new();

    public LuceneSearchIndex(LuceneSearchOptions options)
    {
        _dir = string.IsNullOrWhiteSpace(options.IndexPath)
            ? new RAMDirectory()
            : FSDirectory.Open(new DirectoryInfo(options.IndexPath));
        _analyzer = new StandardAnalyzer(V);
        _writer = new IndexWriter(_dir, new IndexWriterConfig(V, _analyzer) { OpenMode = OpenMode.CREATE_OR_APPEND });
        _writer.Commit(); // materialize an empty index so the first reader can open
    }

    public Task IndexAsync(SearchDocument document, CancellationToken ct = default)
    {
        lock (_gate) { Upsert(document); _writer.Commit(); }
        return Task.CompletedTask;
    }

    public Task IndexManyAsync(IEnumerable<SearchDocument> documents, CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (var d in documents) { ct.ThrowIfCancellationRequested(); Upsert(d); }
            _writer.Commit();
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        lock (_gate) { _writer.DeleteDocuments(new Term("id", id)); _writer.Commit(); }
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        lock (_gate) { _writer.DeleteAll(); _writer.Commit(); }
        return Task.CompletedTask;
    }

    public Task<long> CountAsync(CancellationToken ct = default)
    {
        lock (_gate) { return Task.FromResult((long)_writer.NumDocs); }
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, string? localeCode = null, int max = 50, CancellationToken ct = default)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q)) return Task.FromResult<IReadOnlyList<SearchHit>>([]);

        DirectoryReader reader;
        lock (_gate) { reader = DirectoryReader.Open(_writer, applyAllDeletes: true); }
        using (reader)
        {
            var searcher = new IndexSearcher(reader);

            Query text;
            // Prefix-as-you-type: parse the escaped query with a trailing '*'. Fall back to a plain
            // title term if the parser rejects the input.
            try { text = new MultiFieldQueryParser(V, QueryFields, _analyzer).Parse(QueryParser.Escape(q) + "*"); }
            catch { text = new TermQuery(new Term("title", q.ToLowerInvariant())); }

            Query final = text;
            if (!string.IsNullOrEmpty(localeCode))
                final = new BooleanQuery
                {
                    { text, Occur.MUST },
                    { new TermQuery(new Term("locale", localeCode)), Occur.MUST },
                };

            var top = searcher.Search(final, max);
            var hits = new List<SearchHit>(top.ScoreDocs.Length);
            foreach (var sd in top.ScoreDocs)
            {
                var doc = searcher.Doc(sd.Doc);
                hits.Add(new SearchHit(
                    doc.Get("id"), doc.Get("kind"), doc.Get("title") ?? string.Empty,
                    doc.Get("subtitle"), doc.Get("href"), doc.Get("tag"), sd.Score));
            }
            return Task.FromResult<IReadOnlyList<SearchHit>>(hits);
        }
    }

    private void Upsert(SearchDocument d)
    {
        var doc = new Document
        {
            new StringField("id", d.Id, Field.Store.YES),
            new StringField("kind", d.Kind, Field.Store.YES),
            new TextField("title", d.Title, Field.Store.YES),
        };
        if (!string.IsNullOrEmpty(d.LocaleCode)) doc.Add(new StringField("locale", d.LocaleCode, Field.Store.YES));
        if (!string.IsNullOrEmpty(d.Body)) doc.Add(new TextField("body", d.Body, Field.Store.NO));
        if (!string.IsNullOrEmpty(d.Subtitle)) doc.Add(new StoredField("subtitle", d.Subtitle));
        if (!string.IsNullOrEmpty(d.Href)) doc.Add(new StoredField("href", d.Href));
        if (!string.IsNullOrEmpty(d.Tag)) doc.Add(new StoredField("tag", d.Tag));
        _writer.UpdateDocument(new Term("id", d.Id), doc);
    }

    public void Dispose()
    {
        _writer.Dispose();
        _analyzer.Dispose();
        _dir.Dispose();
    }
}
