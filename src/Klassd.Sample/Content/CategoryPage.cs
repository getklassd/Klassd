using Klassd.Core.Abstractions;

namespace Klassd.Sample.Content;

[CmsPage(Icon = "folder")]
[AllowedChildren(typeof(CategoryPage), typeof(ContentPage))]
public class CategoryPage : PageBase
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public BlockArea PageBlocks { get; set; } = new();
}
