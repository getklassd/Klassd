using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Abstractions.Media;

/// <summary>Configures media sections. Each section binds a name to a blob adapter via <c>UseXxx</c>.</summary>
public interface IMediaBuilder
{
    ICmsBuilder Cms { get; }
    IMediaBuilder AddSection(string name, Action<IMediaSectionBuilder> configure);
}

/// <summary>
/// One media section under configuration. Blob adapter packages add extension methods
/// (<c>UseFileSystem</c>/<c>UseS3</c>/<c>UseGoogleCloudStorage</c>) that register a keyed
/// <see cref="IBlobStore"/> for <see cref="Name"/>.
/// </summary>
public interface IMediaSectionBuilder
{
    IServiceCollection Services { get; }
    string Name { get; }
    IMediaSectionBuilder AllowContentTypes(params string[] contentTypes);
    /// <summary>Resize images in-browser so the longest edge is ≤ <paramref name="maxEdgePixels"/> before upload.</summary>
    IMediaSectionBuilder ResizeImages(int maxEdgePixels);
}

internal sealed class MediaBuilder(ICmsBuilder cms) : IMediaBuilder
{
    public ICmsBuilder Cms => cms;
    public List<MediaSection> Sections { get; } = [];

    public IMediaBuilder AddSection(string name, Action<IMediaSectionBuilder> configure)
    {
        var section = new MediaSectionBuilder(cms.Services, name);
        configure(section);
        Sections.Add(new MediaSection(name, section.AllowedContentTypes, section.MaxImageEdge));
        return this;
    }
}

internal sealed class MediaSectionBuilder(IServiceCollection services, string name) : IMediaSectionBuilder
{
    public IServiceCollection Services => services;
    public string Name => name;
    public List<string> AllowedContentTypes { get; } = [];
    public int? MaxImageEdge { get; private set; }

    public IMediaSectionBuilder AllowContentTypes(params string[] contentTypes)
    {
        AllowedContentTypes.AddRange(contentTypes);
        return this;
    }

    public IMediaSectionBuilder ResizeImages(int maxEdgePixels)
    {
        MaxImageEdge = maxEdgePixels;
        return this;
    }
}
