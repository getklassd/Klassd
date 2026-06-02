using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Klassd.Abstractions.Media;

namespace Klassd.Media.S3;

/// <summary>
/// <see cref="IBlobStore"/> backed by Amazon S3 or an S3-compatible service (MinIO / LocalStack).
/// Builds a single <see cref="AmazonS3Client"/> from the options.
/// </summary>
public sealed class S3BlobStore : IBlobStore
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly string _prefix;

    public S3BlobStore(S3BlobOptions options)
    {
        _bucket = options.Bucket;
        _prefix = options.Prefix ?? "";

        var config = new AmazonS3Config();
        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            // S3-compatible backend: explicit endpoint + path-style addressing.
            config.ServiceURL = options.ServiceUrl;
            config.ForcePathStyle = options.ForcePathStyle;
        }
        else if (!string.IsNullOrWhiteSpace(options.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }

        _client = options.AccessKey is { Length: > 0 }
            ? new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config)
            : new AmazonS3Client(config);
    }

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) =>
        _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = _prefix + key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, ct);

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucket,
                Key = _prefix + key,
            }, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct = default) =>
        _client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucket,
            Key = _prefix + key,
        }, ct);
}
