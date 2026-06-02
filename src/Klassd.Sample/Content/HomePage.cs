using Klassd.Core.Abstractions;

namespace Klassd.Sample.Content;

[CmsPage(DefaultSlug = "", Icon = "house")]
[AllowedChildren(typeof(ContentPage), typeof(CategoryPage))]
public class HomePage : PageBase
{
    [Localized] // Title has separate values per locale; SubTitle does not.
    public string Title { get; set; } = string.Empty;
    public string SubTitle { get; set; } = string.Empty;
    public BlockArea HeroBlocks { get; set; } = new();
    public BlockArea ContentBlocks { get; set; } = new();
}
