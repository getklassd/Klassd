using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Klassd.Media.S3;
using Testcontainers.Minio;
using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>
/// Exercises the real <see cref="S3BlobStore"/> against a throwaway MinIO container
/// (S3-compatible, path-style). Requires Docker — skipped automatically when unavailable.
/// </summary>
[SkipWhenDockerUnavailable]
public class S3BlobStoreTests
{
    private static MinioContainer? _container;

    [Before(HookType.Class)]
    public static async Task StartAsync()
    {
        if (!DockerProbe.IsAvailable()) return;

        // AWS SDK v4 enables flexible checksums by default (CRC32 trailing checksum +
        // STREAMING-…-TRAILER payloads), which this MinIO release rejects with a
        // 'x-amz-content-sha256' mismatch. Opt out for the test process only; real AWS S3
        // and current MinIO both accept the default, so production code stays untouched.
        Environment.SetEnvironmentVariable("AWS_REQUEST_CHECKSUM_CALCULATION", "when_required");
        Environment.SetEnvironmentVariable("AWS_RESPONSE_CHECKSUM_VALIDATION", "when_required");

        _container = new MinioBuilder("minio/minio:RELEASE.2023-01-31T02-24-19Z").Build();
        await _container.StartAsync();
    }

    [After(HookType.Class)]
    public static async Task StopAsync()
    {
        if (_container is not null) await _container.DisposeAsync();
    }

    private static S3BlobOptions Options(string bucket) => new()
    {
        Bucket = bucket,
        ServiceUrl = _container!.GetConnectionString(),
        ForcePathStyle = true,
        AccessKey = _container.GetAccessKey(),
        SecretKey = _container.GetSecretKey(),
    };

    /// <summary>Creates a fresh bucket (the store assumes the bucket already exists).</summary>
    private static async Task<string> NewBucketAsync()
    {
        var bucket = "b" + Guid.NewGuid().ToString("N")[..16];
        using var client = new AmazonS3Client(
            new BasicAWSCredentials(_container!.GetAccessKey(), _container.GetSecretKey()),
            new AmazonS3Config { ServiceURL = _container.GetConnectionString(), ForcePathStyle = true });
        await client.PutBucketAsync(bucket);
        return bucket;
    }

    private static Stream Bytes(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    [Test]
    public async Task Put_then_open_round_trips_bytes()
    {
        var store = new S3BlobStore(Options(await NewBucketAsync()));
        await store.PutAsync("k.txt", Bytes("hi minio"), "text/plain");

        await using var stream = await store.OpenReadAsync("k.txt");
        await Assert.That(stream).IsNotNull();
        using var reader = new StreamReader(stream!);
        await Assert.That(await reader.ReadToEndAsync()).IsEqualTo("hi minio");
    }

    [Test]
    public async Task Open_missing_returns_null()
    {
        var store = new S3BlobStore(Options(await NewBucketAsync()));
        await Assert.That(await store.OpenReadAsync("nope.txt")).IsNull();
    }

    [Test]
    public async Task Delete_removes_the_blob()
    {
        var store = new S3BlobStore(Options(await NewBucketAsync()));
        await store.PutAsync("x.txt", Bytes("x"), "text/plain");
        await store.DeleteAsync("x.txt");
        await Assert.That(await store.OpenReadAsync("x.txt")).IsNull();
    }

    [Test]
    public async Task Prefix_is_applied_to_keys()
    {
        var bucket = await NewBucketAsync();
        var opts = Options(bucket);
        opts.Prefix = "uploads/";
        var store = new S3BlobStore(opts);

        await store.PutAsync("y.txt", Bytes("prefixed"), "text/plain");

        // The store transparently prepends the prefix on read.
        await using var stream = await store.OpenReadAsync("y.txt");
        await Assert.That(stream).IsNotNull();

        // And the object physically lives under the prefix.
        using var client = new AmazonS3Client(
            new BasicAWSCredentials(_container!.GetAccessKey(), _container.GetSecretKey()),
            new AmazonS3Config { ServiceURL = _container.GetConnectionString(), ForcePathStyle = true });
        var listed = await client.ListObjectsV2Async(new Amazon.S3.Model.ListObjectsV2Request { BucketName = bucket });
        await Assert.That(listed.S3Objects.Single().Key).IsEqualTo("uploads/y.txt");
    }
}
