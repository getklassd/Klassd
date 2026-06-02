using Klassd.Abstractions;
using Klassd.Abstractions.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Cache.InMemory;

public static class InMemoryCacheExtensions
{
    /// <summary>
    /// Enables the read-through page cache backed by an in-process <see cref="MemoryCmsCache"/>.
    /// Call AFTER a storage adapter (e.g. <c>UseSqlite</c>).
    /// </summary>
    public static ICmsBuilder UseInMemoryCache(this ICmsBuilder cms, Action<CmsCacheOptions>? configure = null)
    {
        var options = new CmsCacheOptions();
        configure?.Invoke(options);
        cms.Services.AddSingleton<ICmsCache, MemoryCmsCache>();
        return cms.AddPageCaching(options);
    }
}
