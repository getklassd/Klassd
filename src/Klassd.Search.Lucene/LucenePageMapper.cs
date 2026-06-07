using System.Text;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Search;
using Klassd.Abstractions.Storage;

namespace Klassd.Search.Lucene;

/// <summary>Maps a <see cref="PageRecord"/> to the <see cref="SearchDocument"/> indexed for it.</summary>
internal static class LucenePageMapper
{
    public static string DocId(string pageId) => "page:" + pageId;

    public static SearchDocument ToDocument(PageRecord page, SearchableFields searchable)
    {
        // Body = slug + every [Indexable] field value, so full-text covers more than the title.
        var body = new StringBuilder(page.Slug);
        foreach (var key in searchable.PageFields)
            if (page.Data.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                body.Append(' ').Append(v);

        return new SearchDocument
        {
            Id = DocId(page.Id),
            Kind = "page",
            LocaleCode = page.LocaleCode,
            Title = string.IsNullOrWhiteSpace(page.Name) ? page.Slug : page.Name,
            Body = body.ToString(),
            Subtitle = "/" + page.Slug,
            Href = $"/admin/pages?edit={page.Id}",
            Tag = page.PageTypeName,
        };
    }
}
