using Klassd.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Abstractions.Caching;

public static class CacheBuilderExtensions
{
    private const string InnerKey = "klassd.pagestore.inner";

    /// <summary>
    /// Decorates the registered <see cref="IPageStore"/> with <see cref="CachingPageStore"/>,
    /// using whatever <see cref="ICmsCache"/> is registered. Call AFTER a storage adapter.
    /// Cache packages (<c>UseInMemoryCache</c>/<c>UseRedisCache</c>) register an
    /// <see cref="ICmsCache"/> and then call this; advanced users can register their own
    /// <see cref="ICmsCache"/> and call this directly.
    /// </summary>
    public static ICmsBuilder AddPageCaching(this ICmsBuilder cms, CmsCacheOptions options)
    {
        cms.Services.AddSingleton(options);

        var descriptor = cms.Services.LastOrDefault(d => d.ServiceType == typeof(IPageStore))
            ?? throw new InvalidOperationException(
                "Caching must be enabled after a storage adapter (UseMongoDb/UsePostgres/UseSqlite).");

        // Re-register the inner store under a key, then expose IPageStore as the decorator.
        if (descriptor.ImplementationType is { } implType)
            cms.Services.Add(ServiceDescriptor.DescribeKeyed(typeof(IPageStore), InnerKey, implType, descriptor.Lifetime));
        else if (descriptor.ImplementationFactory is { } factory)
            cms.Services.Add(ServiceDescriptor.DescribeKeyed(typeof(IPageStore), InnerKey, (sp, _) => factory(sp), descriptor.Lifetime));
        else
            throw new InvalidOperationException("Unsupported IPageStore registration; cannot decorate with caching.");

        cms.Services.Remove(descriptor);
        cms.Services.Add(ServiceDescriptor.Describe(
            typeof(IPageStore),
            sp => new CachingPageStore(
                sp.GetRequiredKeyedService<IPageStore>(InnerKey),
                sp.GetRequiredService<ICmsCache>(),
                sp.GetRequiredService<CmsCacheOptions>()),
            descriptor.Lifetime));

        return cms;
    }
}
