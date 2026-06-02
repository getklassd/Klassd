using Klassd.Abstractions.Media;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;

namespace Klassd.Media.GoogleCloud;

/// <summary>
/// <see cref="IBlobStore"/> backed by Google Cloud Storage. Builds a single <see cref="StorageClient"/>
/// from the options (service-account JSON/file, default credentials, or a local emulator).
/// </summary>
public sealed class GcsBlobStore : IBlobStore
{
    private readonly StorageClient _client;
    private readonly string _bucket;
    private readonly string _prefix;

    public GcsBlobStore(GcsBlobOptions options)
    {
        _bucket = options.Bucket;
        _prefix = options.Prefix ?? "";

        if (!string.IsNullOrWhiteSpace(options.StorageEmulatorHost))
        {
            // Local emulator (e.g. fake-gcs-server): point the client at it and skip auth.
            _client = new StorageClientBuilder
            {
                BaseUri = $"http://{options.StorageEmulatorHost}/storage/v1/",
                UnauthenticatedAccess = true,
            }.Build();
        }
        else if (!string.IsNullOrWhiteSpace(options.CredentialsJson))
        {
            // GoogleCredential.FromJson auto-detects the credential kind (service account, user,
            // external account) from the payload. The non-obsolete CredentialFactory.FromJson<T>
            // requires the concrete type up front, which we don't have for caller-supplied JSON.
#pragma warning disable CS0618 // Type or member is obsolete
            _client = StorageClient.Create(GoogleCredential.FromJson(options.CredentialsJson));
#pragma warning restore CS0618
        }
        else if (!string.IsNullOrWhiteSpace(options.CredentialsPath))
        {
#pragma warning disable CS0618 // Type or member is obsolete
            _client = StorageClient.Create(GoogleCredential.FromFile(options.CredentialsPath));
#pragma warning restore CS0618
        }
        else
        {
            _client = StorageClient.Create();
        }
    }

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) =>
        _client.UploadObjectAsync(_bucket, _prefix + key, contentType, content, cancellationToken: ct);

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var stream = new MemoryStream();
            await _client.DownloadObjectAsync(_bucket, _prefix + key, stream, cancellationToken: ct);
            stream.Position = 0;
            return stream;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _client.DeleteObjectAsync(_bucket, _prefix + key, cancellationToken: ct);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Already gone — treat as success.
        }
    }
}
