using Klassd.Abstractions;
using Klassd.Abstractions.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Cache.Hybrid;

public static class HybridCacheExtensions
{
    /// <summary>
    /// Enables the read-through page cache backed by Microsoft's <see cref="HybridCache"/>
    /// (fast in-process L1 + an optional distributed L2). Call AFTER a storage adapter.
    ///
    /// <para>For a shared L2 tier, register an <c>IDistributedCache</c> before this call (e.g.
    /// <c>services.AddStackExchangeRedisCache(...)</c>); HybridCache picks it up automatically.
    /// With no L2 registered it runs L1-only. Plug a custom serializer or backend via
    /// <paramref name="build"/> (the standard <see cref="IHybridCacheBuilder"/> seam).</para>
    /// </summary>
    public static ICmsBuilder UseHybridCache(
        this ICmsBuilder cms,
        Action<CmsCacheOptions>? configure = null,
        Action<HybridCacheOptions>? configureHybrid = null,
        Action<IHybridCacheBuilder>? build = null)
    {
        var options = new CmsCacheOptions();
        configure?.Invoke(options);

        var builder = configureHybrid is null
            ? cms.Services.AddHybridCache()
            : cms.Services.AddHybridCache(configureHybrid);
        build?.Invoke(builder);

        cms.Services.AddSingleton<ICmsCache, HybridCmsCache>();
        return cms.AddPageCaching(options);
    }
}
