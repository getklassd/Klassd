using Klassd.Core.Abstractions;

namespace Klassd.Sample.Content;

public class ContentPage : PageBase
{
    public string Title { get; set; } = string.Empty;

    [CmsField(FieldType = "textarea")]
    public string Content { get; set; } = string.Empty;

    public BlockArea PageBlocks { get; set; } = new();
}
