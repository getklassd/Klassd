using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Abstractions.Media;

public static class MediaBuilderExtensions
{
    /// <summary>
    /// Configures media sections. Each <c>AddSection(name, s => s.UseXxx(...))</c> binds a section
    /// to a blob adapter (registered as a keyed <see cref="IBlobStore"/>). Overrides the empty
    /// section registry that AddKlassd installs by default. Call after AddKlassd.
    /// </summary>
    public static ICmsBuilder AddMedia(this ICmsBuilder cms, Action<IMediaBuilder> configure)
    {
        var builder = new MediaBuilder(cms);
        configure(builder);
        cms.Services.AddSingleton(new MediaSectionRegistry(builder.Sections));
        return cms;
    }
}
