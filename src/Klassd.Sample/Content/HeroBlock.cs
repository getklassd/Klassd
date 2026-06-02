using Klassd.Core.Abstractions;

namespace Klassd.Sample.Content;

public class HeroBlock : BlockBase
{
    public string Heading { get; set; } = string.Empty;
    public string SubHeading { get; set; } = string.Empty;
    public string ButtonText { get; set; } = string.Empty;

    [CmsField(FieldType = "color")] // uses the custom property type registered in Program.cs
    public string BackgroundColor { get; set; } = string.Empty;

    [CmsField(FieldType = "media")] // media picker; stores the selected media item's id
    public string Image { get; set; } = string.Empty;

    public string ButtonUrl { get; set; } = string.Empty;
}
