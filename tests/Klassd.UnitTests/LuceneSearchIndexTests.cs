using Klassd.Abstractions.Search;
using Klassd.Search.Lucene;
using TUnit.Core;

namespace Klassd.UnitTests;

// In-memory (RAMDirectory) Lucene index — no disk, no external infra.
public class LuceneSearchIndexTests
{
    private static LuceneSearchIndex NewIndex() => new(new LuceneSearchOptions()); // null IndexPath ⇒ RAMDirectory

    private static SearchDocument Doc(string id, string title, string? body = null, string? locale = "en", string? tag = "ContentPage") =>
        new() { Id = id, Kind = "page", LocaleCode = locale, Title = title, Body = body, Subtitle = "/" + id, Href = "/admin/pages?edit=" + id, Tag = tag };

    [Test]
    public async Task Indexes_and_finds_by_title()
    {
        using var index = NewIndex();
        await index.IndexAsync(Doc("1", "Welcome Home"));

        var hits = await index.SearchAsync("welcome");

        await Assert.That(hits.Count).IsEqualTo(1);
        await Assert.That(hits[0].Id).IsEqualTo("1");
        await Assert.That(hits[0].Tag).IsEqualTo("ContentPage");
        await Assert.That(hits[0].Subtitle).IsEqualTo("/1");
    }

    [Test]
    public async Task Finds_by_body_content()
    {
        using var index = NewIndex();
        await index.IndexAsync(Doc("1", "About", body: "our pricing and plans"));

        var hits = await index.SearchAsync("pricing");

        await Assert.That(hits.Select(h => h.Id)).IsEquivalentTo(["1"]);
    }

    [Test]
    public async Task Prefix_matches_as_you_type()
    {
        using var index = NewIndex();
        await index.IndexAsync(Doc("1", "Documentation"));

        await Assert.That((await index.SearchAsync("docu")).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Upsert_replaces_same_id()
    {
        using var index = NewIndex();
        await index.IndexAsync(Doc("1", "First Title"));
        await index.IndexAsync(Doc("1", "Second Title"));

        await Assert.That(await index.CountAsync()).IsEqualTo(1);
        await Assert.That((await index.SearchAsync("second")).Count).IsEqualTo(1);
        await Assert.That((await index.SearchAsync("first")).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Delete_removes_document()
    {
        using var index = NewIndex();
        await index.IndexAsync(Doc("1", "Gone Soon"));
        await index.DeleteAsync("1");

        await Assert.That(await index.CountAsync()).IsEqualTo(0);
        await Assert.That((await index.SearchAsync("gone")).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Locale_filter_excludes_other_locales()
    {
        using var index = NewIndex();
        await index.IndexAsync(Doc("en1", "Contact", locale: "en"));
        await index.IndexAsync(Doc("da1", "Contact", locale: "da"));

        var en = await index.SearchAsync("contact", localeCode: "en");

        await Assert.That(en.Select(h => h.Id)).IsEquivalentTo(["en1"]);
    }

    [Test]
    public async Task Clear_empties_the_index()
    {
        using var index = NewIndex();
        await index.IndexManyAsync([Doc("1", "A"), Doc("2", "B")]);
        await index.ClearAsync();

        await Assert.That(await index.CountAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task Empty_query_returns_nothing()
    {
        using var index = NewIndex();
        await index.IndexAsync(Doc("1", "Anything"));

        await Assert.That((await index.SearchAsync("  ")).Count).IsEqualTo(0);
    }
}
