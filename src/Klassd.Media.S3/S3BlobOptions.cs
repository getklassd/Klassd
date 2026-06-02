namespace Klassd.Media.S3;

/// <summary>Options for <see cref="S3BlobStore"/>.</summary>
public sealed class S3BlobOptions
{
    /// <summary>Target bucket (must already exist).</summary>
    public string Bucket { get; set; } = "";

    /// <summary>AWS region system name (e.g. <c>eu-west-1</c>). Ignored when <see cref="ServiceUrl"/> is set.</summary>
    public string? Region { get; set; }

    /// <summary>Access key. When set, used with <see cref="SecretKey"/>; otherwise the default credential chain is used.</summary>
    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    /// <summary>Custom endpoint for S3-compatible backends (MinIO / LocalStack), e.g. <c>http://localhost:9000</c>.</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Use path-style addressing (required by most S3-compatible backends like MinIO).</summary>
    public bool ForcePathStyle { get; set; }

    /// <summary>Optional key prefix prepended to every blob key.</summary>
    public string? Prefix { get; set; }
}
