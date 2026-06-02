namespace Klassd.Abstractions.Media;

/// <summary>
/// A named media section. <see cref="AllowedContentTypes"/> empty = any type allowed.
/// <see cref="MaxImageEdge"/> (px), when set, is a hint for the admin to resize images
/// in-browser (longest edge) before upload; null = no resize.
/// </summary>
public sealed record MediaSection(string Name, IReadOnlyList<string> AllowedContentTypes, int? MaxImageEdge = null);

/// <summary>The configured media sections (populated by <c>AddMedia</c>).</summary>
public sealed class MediaSectionRegistry(IReadOnlyList<MediaSection> sections)
{
    public IReadOnlyList<MediaSection> Sections { get; } = sections;
    public bool Exists(string name) => Sections.Any(s => s.Name == name);
    public MediaSection? Get(string name) => Sections.FirstOrDefault(s => s.Name == name);
}
