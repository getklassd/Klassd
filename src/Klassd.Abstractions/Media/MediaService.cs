using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Abstractions.Media;

/// <summary>
/// Orchestrates media: writes bytes to the section's <see cref="IBlobStore"/> and metadata
/// to <see cref="IMediaStore"/>. Resolves the per-section blob store via keyed DI.
/// </summary>
public sealed class MediaService(IMediaStore store, MediaSectionRegistry sections, IServiceProvider serviceProvider)
{
    public IReadOnlyList<MediaSection> Sections => sections.Sections;

    public Task<IReadOnlyList<MediaRecord>> ListAsync(string section, CancellationToken ct = default) =>
        store.ListAsync(section, ct);

    public Task<MediaRecord?> GetAsync(string id, CancellationToken ct = default) =>
        store.GetAsync(id, ct);

    public async Task<MediaRecord> UploadAsync(
        string section, string fileName, string contentType, long size, Stream content, CancellationToken ct = default)
    {
        var sec = sections.Get(section)
            ?? throw new InvalidOperationException($"Unknown media section '{section}'.");

        if (sec.AllowedContentTypes.Count > 0 && !sec.AllowedContentTypes.Any(p => ContentTypeMatches(p, contentType)))
            throw new InvalidOperationException(
                $"Content type '{contentType}' is not allowed in media section '{section}'.");

        var id = Guid.NewGuid().ToString();
        var key = id + Path.GetExtension(fileName);

        await Blob(section).PutAsync(key, content, contentType, ct);

        var record = new MediaRecord
        {
            Id = id,
            Section = section,
            Key = key,
            FileName = fileName,
            ContentType = contentType,
            Size = size,
            UploadedAt = DateTime.UtcNow,
        };
        await store.InsertAsync(record, ct);
        return record;
    }

    /// <summary>Updates editable metadata (display name, alt text, focal points, custom data). Returns updated record or null if missing.</summary>
    public async Task<MediaRecord?> UpdateMetadataAsync(
        string id, string? displayName, string? altText, List<MediaFocalPoint>? focalPoints, Dictionary<string, string>? data, CancellationToken ct = default)
    {
        var record = await store.GetAsync(id, ct);
        if (record is null) return null;
        // Normalize empty/whitespace to null so the UI/headless callers all get the same FileName fallback.
        record.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        record.AltText = altText;
        if (focalPoints is not null) record.FocalPoints = focalPoints;
        if (data is not null) record.Data = data;
        return await store.UpdateAsync(record, ct);
    }

    /// <summary>Returns the record + an open read stream for its bytes, or null if missing.</summary>
    public async Task<(MediaRecord Record, Stream Stream)?> OpenAsync(string id, CancellationToken ct = default)
    {
        var record = await store.GetAsync(id, ct);
        if (record is null) return null;
        var stream = await Blob(record.Section).OpenReadAsync(record.Key, ct);
        return stream is null ? null : (record, stream);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var record = await store.GetAsync(id, ct);
        if (record is null) return false;
        await Blob(record.Section).DeleteAsync(record.Key, ct);
        return await store.DeleteAsync(id, ct);
    }

    /// <summary>The streaming URL the admin/SPA uses to fetch a media item.</summary>
    public static string UrlFor(string id) => $"/api/media/{id}";

    private IBlobStore Blob(string section) =>
        serviceProvider.GetKeyedService<IBlobStore>(section)
            ?? throw new InvalidOperationException(
                $"Media section '{section}' has no storage adapter. Configure it with AddMedia(m => m.AddSection(\"{section}\", s => s.UseXxx(...))).");

    private static bool ContentTypeMatches(string pattern, string contentType)
    {
        if (pattern.EndsWith("/*", StringComparison.Ordinal))
            return contentType.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase); // "image/" prefix
        return string.Equals(pattern, contentType, StringComparison.OrdinalIgnoreCase);
    }
}
