using Klassd.Abstractions.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Media.GoogleCloud;

public static class GcsMediaExtensions
{
    /// <summary>Backs this media section with Google Cloud Storage, registering a keyed <see cref="IBlobStore"/>.</summary>
    public static IMediaSectionBuilder UseGoogleCloudStorage(this IMediaSectionBuilder builder, Action<GcsBlobOptions> configure)
    {
        var options = new GcsBlobOptions();
        configure(options);

        builder.Services.AddKeyedSingleton<IBlobStore>(builder.Name, (_, _) => new GcsBlobStore(options));
        return builder;
    }
}
