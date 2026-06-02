namespace Klassd.Abstractions.Caching;

/// <summary>Options for the optional page caching layer.</summary>
public sealed class CmsCacheOptions
{
    /// <summary>Time-to-live for cached page reads. Null = no expiry (relies on write invalidation). Default 5 min.</summary>
    public TimeSpan? Ttl { get; set; } = TimeSpan.FromMinutes(5);
}
