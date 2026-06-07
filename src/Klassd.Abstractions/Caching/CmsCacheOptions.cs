namespace Klassd.Abstractions.Caching;

/// <summary>Options for the optional page caching layer.</summary>
public sealed class CmsCacheOptions
{
    /// <summary>Time-to-live for cached page reads. Null = no expiry (relies on write invalidation). Default 5 min.</summary>
    public TimeSpan? Ttl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Time-to-live for the local (L1) tier of the hybrid cache. Ignored by the single-tier
    /// in-memory and Redis caches. A write on one node invalidates that node's L1 immediately,
    /// but other nodes' L1 entries persist until this expires — keep it short to bound staleness.
    /// Null falls back to <see cref="Ttl"/>. Default 1 min.
    /// </summary>
    public TimeSpan? LocalTtl { get; set; } = TimeSpan.FromMinutes(1);
}
