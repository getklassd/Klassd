namespace Klassd.Media.FileSystem;

/// <summary>Options for <see cref="FileSystemBlobStore"/>.</summary>
public sealed class FileSystemBlobOptions
{
    /// <summary>Directory under which this section's blobs are stored.</summary>
    public string RootPath { get; set; } = "";
}
