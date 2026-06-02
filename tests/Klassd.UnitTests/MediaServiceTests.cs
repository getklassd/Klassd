using System.Text;
using Klassd.Abstractions.Media;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;

namespace Klassd.UnitTests;

/// <summary>
/// MediaService orchestration against in-memory fakes for IBlobStore + IMediaStore. The
/// per-section blob store is resolved via keyed DI, mirroring the real registration.
/// </summary>
public class MediaServiceTests
{
    private static readonly MediaSection Images = new("images", ["image/*"]);
    private static readonly MediaSection Any = new("files", []);

    private static (MediaService Svc, FakeBlobStore Blob, InMemoryMediaStore Store) Build(
        IReadOnlyList<MediaSection> registry, params string[] blobSections)
    {
        var store = new InMemoryMediaStore();
        var blob = new FakeBlobStore();
        var services = new ServiceCollection();
        foreach (var name in blobSections)
            services.AddKeyedSingleton<IBlobStore>(name, blob);
        var sp = services.BuildServiceProvider();
        return (new MediaService(store, new MediaSectionRegistry(registry), sp), blob, store);
    }

    private static Stream Bytes(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    [Test]
    public async Task Upload_writes_blob_and_metadata()
    {
        var (svc, blob, store) = Build([Images], "images");

        var rec = await svc.UploadAsync("images", "cat.png", "image/png", 5, Bytes("hello"));

        await Assert.That(rec.Section).IsEqualTo("images");
        await Assert.That(rec.FileName).IsEqualTo("cat.png");
        await Assert.That(rec.ContentType).IsEqualTo("image/png");
        await Assert.That(rec.Size).IsEqualTo(5L);
        await Assert.That(rec.Key).EndsWith(".png");            // {id}.png
        await Assert.That(rec.Key).StartsWith(rec.Id);
        await Assert.That(blob.Blobs.ContainsKey(rec.Key)).IsTrue();
        await Assert.That(store.Records.Single().Id).IsEqualTo(rec.Id);
    }

    [Test]
    public async Task Upload_rejects_disallowed_content_type()
    {
        var (svc, blob, _) = Build([Images], "images");

        await Assert.That(async () => await svc.UploadAsync("images", "doc.pdf", "application/pdf", 3, Bytes("pdf")))
            .Throws<InvalidOperationException>();
        await Assert.That(blob.PutCalls).IsEqualTo(0);          // nothing written on rejection
    }

    [Test]
    public async Task Upload_allows_any_content_type_when_section_unrestricted()
    {
        var (svc, _, _) = Build([Any], "files");

        var rec = await svc.UploadAsync("files", "report.pdf", "application/pdf", 3, Bytes("pdf"));
        await Assert.That(rec.ContentType).IsEqualTo("application/pdf");
    }

    [Test]
    public async Task Upload_unknown_section_throws()
    {
        var (svc, _, _) = Build([Images], "images");

        await Assert.That(async () => await svc.UploadAsync("ghost", "x.png", "image/png", 1, Bytes("x")))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Upload_to_section_without_adapter_throws_helpful_error()
    {
        // Section exists in the registry, but no keyed IBlobStore was registered for it.
        var (svc, _, _) = Build([Images]);

        await Assert.That(async () => await svc.UploadAsync("images", "x.png", "image/png", 1, Bytes("x")))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Open_returns_record_and_byte_stream()
    {
        var (svc, _, _) = Build([Images], "images");
        var rec = await svc.UploadAsync("images", "cat.png", "image/png", 5, Bytes("hello"));

        var opened = await svc.OpenAsync(rec.Id);
        await Assert.That(opened).IsNotNull();
        await Assert.That(opened!.Value.Record.Id).IsEqualTo(rec.Id);
        using var reader = new StreamReader(opened.Value.Stream);
        await Assert.That(await reader.ReadToEndAsync()).IsEqualTo("hello");
    }

    [Test]
    public async Task Open_missing_returns_null()
    {
        var (svc, _, _) = Build([Images], "images");
        await Assert.That(await svc.OpenAsync("nope")).IsNull();
    }

    [Test]
    public async Task Delete_removes_blob_and_metadata()
    {
        var (svc, blob, store) = Build([Images], "images");
        var rec = await svc.UploadAsync("images", "cat.png", "image/png", 5, Bytes("hello"));

        await Assert.That(await svc.DeleteAsync(rec.Id)).IsTrue();
        await Assert.That(blob.Blobs.ContainsKey(rec.Key)).IsFalse();
        await Assert.That(store.Records).IsEmpty();
    }

    [Test]
    public async Task Delete_missing_returns_false()
    {
        var (svc, _, _) = Build([Images], "images");
        await Assert.That(await svc.DeleteAsync("nope")).IsFalse();
    }

    [Test]
    public async Task UpdateMetadata_sets_alt_focal_and_data()
    {
        var (svc, _, _) = Build([Images], "images");
        var rec = await svc.UploadAsync("images", "cat.png", "image/png", 5, Bytes("hello"));

        var updated = await svc.UpdateMetadataAsync(
            rec.Id,
            altText: "A cat",
            focalPoints: [new MediaFocalPoint { Breakpoint = "mobile", X = 0.25, Y = 0.75 }],
            data: new Dictionary<string, string> { ["credit"] = "Jane" });

        await Assert.That(updated).IsNotNull();
        await Assert.That(updated!.AltText).IsEqualTo("A cat");
        await Assert.That(updated.FocalPoints.Single().Breakpoint).IsEqualTo("mobile");
        await Assert.That(updated.FocalPoints.Single().X).IsEqualTo(0.25);
        await Assert.That(updated.Data["credit"]).IsEqualTo("Jane");
    }

    [Test]
    public async Task UpdateMetadata_missing_returns_null()
    {
        var (svc, _, _) = Build([Images], "images");
        await Assert.That(await svc.UpdateMetadataAsync("nope", "alt", null, null)).IsNull();
    }

    [Test]
    public async Task List_filters_by_section()
    {
        var (svc, _, _) = Build([Images, Any], "images", "files");
        await svc.UploadAsync("images", "a.png", "image/png", 1, Bytes("a"));
        await svc.UploadAsync("images", "b.png", "image/png", 1, Bytes("b"));
        await svc.UploadAsync("files", "c.pdf", "application/pdf", 1, Bytes("c"));

        await Assert.That((await svc.ListAsync("images")).Count).IsEqualTo(2);
        await Assert.That((await svc.ListAsync("files")).Count).IsEqualTo(1);
    }

    [Test]
    public async Task UrlFor_builds_the_streaming_path()
    {
        await Assert.That(MediaService.UrlFor("abc")).IsEqualTo("/api/media/abc");
    }
}

/// <summary>In-memory IBlobStore: keeps blob bytes in a dictionary and counts writes/deletes.</summary>
internal sealed class FakeBlobStore : IBlobStore
{
    public readonly Dictionary<string, byte[]> Blobs = new();
    public int PutCalls { get; private set; }
    public int DeleteCalls { get; private set; }

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        PutCalls++;
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        Blobs[key] = ms.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(Blobs.TryGetValue(key, out var b) ? new MemoryStream(b) : null);

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        DeleteCalls++;
        Blobs.Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>In-memory IMediaStore over a list.</summary>
internal sealed class InMemoryMediaStore : IMediaStore
{
    public readonly List<MediaRecord> Records = new();

    public Task<IReadOnlyList<MediaRecord>> ListAsync(string section, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MediaRecord>>(Records.Where(r => r.Section == section).ToList());

    public Task<MediaRecord?> GetAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(Records.FirstOrDefault(r => r.Id == id));

    public Task InsertAsync(MediaRecord media, CancellationToken ct = default)
    {
        Records.Add(media);
        return Task.CompletedTask;
    }

    public Task<MediaRecord?> UpdateAsync(MediaRecord media, CancellationToken ct = default)
    {
        var idx = Records.FindIndex(r => r.Id == media.Id);
        if (idx < 0) return Task.FromResult<MediaRecord?>(null);
        Records[idx] = media;
        return Task.FromResult<MediaRecord?>(media);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(Records.RemoveAll(r => r.Id == id) > 0);
}
