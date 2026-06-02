namespace Klassd.Abstractions.Media;

/// <summary>
/// Binary blob backend for one media section (its own bucket / path / prefix). Implemented
/// by storage packages (FileSystem, S3, GoogleCloud) and registered per section (keyed by
/// section name). Content type is tracked in <see cref="IMediaStore"/>, so reads return only a stream.
/// </summary>
public interface IBlobStore
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Opens the blob for reading, or null if it does not exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}
