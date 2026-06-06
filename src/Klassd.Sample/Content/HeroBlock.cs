using Klassd.Core.Abstractions;

namespace Klassd.Sample.Content;

public class HeroBlock : BlockBase
{
    public string Heading { get; set; } = string.Empty;
    public string SubHeading { get; set; } = string.Empty;
    public string ButtonText { get; set; } = string.Empty;

    [CmsField(FieldType = "color")] // uses the custom property type registered in Program.cs
    public string BackgroundColor { get; set; } = string.Empty;

    // Strongly-typed media reference — auto-maps to the media picker (no [CmsField] needed).
    // Stores the selected media item's id. A `string` with [CmsField(FieldType="media")] also works.
    public MediaReference Image { get; set; } = new();

    public string ButtonUrl { get; set; } = string.Empty;
}
