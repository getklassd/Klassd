using Klassd.Abstractions.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Media.FileSystem;

public static class FileSystemMediaExtensions
{
    /// <summary>Backs this media section with the local file system, registering a keyed <see cref="IBlobStore"/>.</summary>
    public static IMediaSectionBuilder UseFileSystem(this IMediaSectionBuilder builder, Action<FileSystemBlobOptions> configure)
    {
        var options = new FileSystemBlobOptions();
        configure(options);

        builder.Services.AddKeyedSingleton<IBlobStore>(builder.Name, (_, _) => new FileSystemBlobStore(options));
        return builder;
    }

    /// <summary>Backs this media section with the local file system rooted at <paramref name="rootPath"/>.</summary>
    public static IMediaSectionBuilder UseFileSystem(this IMediaSectionBuilder builder, string rootPath) =>
        builder.UseFileSystem(o => o.RootPath = rootPath);
}
