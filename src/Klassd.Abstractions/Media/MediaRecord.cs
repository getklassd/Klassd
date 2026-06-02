namespace Klassd.Abstractions.Media;

/// <summary>DB-agnostic metadata for one stored media item. Bytes live in the section's <see cref="IBlobStore"/>.</summary>
public sealed class MediaRecord
{
    public string Id { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    /// <summary>Object key within the section's blob store (e.g. "{id}.png").</summary>
    public string Key { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? AltText { get; set; }

    /// <summary>Focal points per named breakpoint, for responsive art-direction (CSS object-position).</summary>
    public List<MediaFocalPoint> FocalPoints { get; set; } = [];

    /// <summary>Open metadata bag for consumer-defined extra fields (stored as JSON).</summary>
    public Dictionary<string, string> Data { get; set; } = new();

    public DateTime UploadedAt { get; set; }
}

/// <summary>
/// A focal point for a named breakpoint (e.g. "default", "mobile"). <see cref="X"/>/<see cref="Y"/>
/// are fractions 0..1 of the image; map to CSS <c>object-position: X*100% Y*100%</c> per media query.
/// </summary>
public sealed class MediaFocalPoint
{
    public string Breakpoint { get; set; } = "default";
    public double X { get; set; } = 0.5;
    public double Y { get; set; } = 0.5;
}
