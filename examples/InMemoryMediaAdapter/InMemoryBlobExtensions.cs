using Klassd.Abstractions.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Examples.InMemoryMedia;

/// <summary>
/// The registration seam. A blob adapter is exposed as a <c>UseXxx</c> extension on
/// <see cref="IMediaSectionBuilder"/> that registers an <see cref="IBlobStore"/> <b>keyed by the
/// section name</b> — that key is how the engine resolves the right backend per section.
/// </summary>
public static class InMemoryBlobExtensions
{
    /// <summary>Backs this media section with an in-memory <see cref="IBlobStore"/>.</summary>
    public static IMediaSectionBuilder UseInMemoryBlobs(this IMediaSectionBuilder builder)
    {
        // Keyed by builder.Name so each section gets its own isolated store instance.
        builder.Services.AddKeyedSingleton<IBlobStore>(builder.Name, (_, _) => new InMemoryBlobStore());
        return builder;
    }
}

// Usage in the host's Program.cs:
//
//   builder.Services
//       .AddKlassd(builder.Configuration)
//       .UseSqlite(builder.Configuration.GetSection("Sqlite"))
//       .AddMedia(media =>
//       {
//           media.AddSection("images", s => s
//               .UseInMemoryBlobs()
//               .AllowContentTypes("image/*"));
//       });
