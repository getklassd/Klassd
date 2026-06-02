namespace Klassd.Media.GoogleCloud;

/// <summary>Options for <see cref="GcsBlobStore"/>.</summary>
public sealed class GcsBlobOptions
{
    /// <summary>Target bucket (must already exist).</summary>
    public string Bucket { get; set; } = "";

    /// <summary>Path to a service-account JSON key file. Mutually exclusive with <see cref="CredentialsJson"/>.</summary>
    public string? CredentialsPath { get; set; }

    /// <summary>Inline service-account JSON. Takes precedence over <see cref="CredentialsPath"/>.</summary>
    public string? CredentialsJson { get; set; }

    /// <summary>Optional key prefix prepended to every blob key.</summary>
    public string? Prefix { get; set; }

    /// <summary>Host:port of a GCS emulator (e.g. fake-gcs-server) for local testing, e.g. <c>localhost:4443</c>.</summary>
    public string? StorageEmulatorHost { get; set; }
}
