using Klassd.Core.Abstractions;

namespace Klassd.Sample.Content;

[CmsPage(Icon = "file")]
public class ContentPage : PageBase
{
    public string Title { get; set; } = string.Empty;

    [CmsField(FieldType = "textarea")]
    public string Content { get; set; } = string.Empty;

    // Relationship field — page picker restricted to ContentPage targets. Stores the
    // linked page's ContentId; omit [AllowedRelations] to allow any page type.
    [CmsField(DisplayName = "Related page")]
    [AllowedRelations(typeof(ContentPage))]
    public PageReference RelatedPage { get; set; } = new();

    public BlockArea PageBlocks { get; set; } = new();
}
