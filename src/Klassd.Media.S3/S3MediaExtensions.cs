using Klassd.Abstractions.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Media.S3;

public static class S3MediaExtensions
{
    /// <summary>Backs this media section with Amazon S3 (or MinIO/LocalStack), registering a keyed <see cref="IBlobStore"/>.</summary>
    public static IMediaSectionBuilder UseS3(this IMediaSectionBuilder builder, Action<S3BlobOptions> configure)
    {
        var options = new S3BlobOptions();
        configure(options);

        builder.Services.AddKeyedSingleton<IBlobStore>(builder.Name, (_, _) => new S3BlobStore(options));
        return builder;
    }
}
