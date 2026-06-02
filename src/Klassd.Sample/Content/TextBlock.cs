using Klassd.Core.Abstractions;

namespace Klassd.Sample.Content;

public class TextBlock : BlockBase
{
    [CmsField(FieldType = "textarea")]
    public string Content { get; set; } = string.Empty;
}
