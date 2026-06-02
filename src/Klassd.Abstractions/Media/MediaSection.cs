namespace Klassd.Abstractions.Media;

/// <summary>
/// A named media section. <see cref="AllowedContentTypes"/> empty = any type allowed.
/// <see cref="MaxImageEdge"/> (px), when set, is a hint for the admin to resize images
/// in-browser (longest edge) before upload; null = no resize.
/// <see cref="Breakpoints"/> are the named focal-point breakpoints offered in the admin; when
/// none are configured the only option is <c>"default"</c> (see <see cref="EffectiveBreakpoints"/>).
/// </summary>
public sealed record MediaSection(
    string Name,
    IReadOnlyList<string> AllowedContentTypes,
    int? MaxImageEdge = null,
    IReadOnlyList<string>? Breakpoints = null)
{
    private static readonly string[] DefaultBreakpoints = ["default"];

    /// <summary>The breakpoints to offer for focal points: the configured list, or <c>["default"]</c> when none.</summary>
    public IReadOnlyList<string> EffectiveBreakpoints =>
        Breakpoints is { Count: > 0 } ? Breakpoints : DefaultBreakpoints;
}

/// <summary>The configured media sections (populated by <c>AddMedia</c>).</summary>
public sealed class MediaSectionRegistry(IReadOnlyList<MediaSection> sections)
{
    public IReadOnlyList<MediaSection> Sections { get; } = sections;
    public bool Exists(string name) => Sections.Any(s => s.Name == name);
    public MediaSection? Get(string name) => Sections.FirstOrDefault(s => s.Name == name);
}
