using Klassd.Abstractions.Media;

namespace Klassd.Media.FileSystem;

/// <summary>
/// <see cref="IBlobStore"/> that stores each blob as a file under <see cref="FileSystemBlobOptions.RootPath"/>.
/// Content type is not persisted (it lives in the media store); reads return the file stream.
/// </summary>
public sealed class FileSystemBlobStore(FileSystemBlobOptions options) : IBlobStore
{
    private readonly string _root = Path.GetFullPath(options.RootPath);

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        Stream? stream = File.Exists(path) ? File.OpenRead(path) : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>Maps a flat blob key to a path under the root, rejecting traversal / rooted keys.</summary>
    private string ResolvePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key)
            || key.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(key))
            throw new ArgumentException($"Invalid blob key '{key}'.", nameof(key));

        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root, StringComparison.Ordinal))
            throw new ArgumentException($"Invalid blob key '{key}'.", nameof(key));

        return path;
    }
}
