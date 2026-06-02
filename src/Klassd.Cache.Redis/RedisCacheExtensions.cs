using Klassd.Abstractions;
using Klassd.Abstractions.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Klassd.Cache.Redis;

public static class RedisCacheExtensions
{
    /// <summary>
    /// Enables the read-through page cache backed by Redis. Call AFTER a storage adapter.
    /// Registers a singleton <see cref="IConnectionMultiplexer"/> from the connection string
    /// (unless one is already registered).
    /// </summary>
    public static ICmsBuilder UseRedisCache(this ICmsBuilder cms, string connectionString, Action<CmsCacheOptions>? configure = null)
    {
        var options = new CmsCacheOptions();
        configure?.Invoke(options);

        cms.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        cms.Services.AddSingleton<ICmsCache, RedisCmsCache>();
        return cms.AddPageCaching(options);
    }

    /// <summary>Reads <c>ConnectionString</c> from the given configuration section.</summary>
    public static ICmsBuilder UseRedisCache(this ICmsBuilder cms, IConfiguration section, Action<CmsCacheOptions>? configure = null) =>
        cms.UseRedisCache(
            section["ConnectionString"] ?? throw new InvalidOperationException("Redis configuration is missing a 'ConnectionString' value."),
            configure);
}
