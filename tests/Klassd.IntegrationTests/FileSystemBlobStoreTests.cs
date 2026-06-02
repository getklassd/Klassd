using System.Text;
using Klassd.Media.FileSystem;
using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>FileSystemBlobStore round-trips through a real temp directory and rejects unsafe keys.</summary>
public class FileSystemBlobStoreTests
{
    private static FileSystemBlobStore Store(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), "cfcms-blob-" + Guid.NewGuid().ToString("N"));
        return new FileSystemBlobStore(new FileSystemBlobOptions { RootPath = root });
    }

    private static Stream Bytes(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    [Test]
    public async Task Put_then_open_round_trips_bytes()
    {
        var store = Store(out var root);
        try
        {
            await store.PutAsync("abc.txt", Bytes("hello world"), "text/plain");

            await using var stream = await store.OpenReadAsync("abc.txt");
            await Assert.That(stream).IsNotNull();
            using var reader = new StreamReader(stream!);
            await Assert.That(await reader.ReadToEndAsync()).IsEqualTo("hello world");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Open_missing_returns_null()
    {
        var store = Store(out var root);
        try
        {
            await Assert.That(await store.OpenReadAsync("nope.txt")).IsNull();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Delete_removes_the_blob()
    {
        var store = Store(out var root);
        try
        {
            await store.PutAsync("x.txt", Bytes("x"), "text/plain");
            await store.DeleteAsync("x.txt");
            await Assert.That(await store.OpenReadAsync("x.txt")).IsNull();
            await store.DeleteAsync("x.txt"); // deleting a missing key is a no-op
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Traversal_and_rooted_keys_are_rejected()
    {
        var store = Store(out var root);
        try
        {
            await Assert.That(async () => await store.PutAsync("../escape.txt", Bytes("x"), "text/plain"))
                .Throws<ArgumentException>();
            await Assert.That(async () => await store.OpenReadAsync("../../etc/passwd"))
                .Throws<ArgumentException>();
            await Assert.That(async () => await store.PutAsync("/abs.txt", Bytes("x"), "text/plain"))
                .Throws<ArgumentException>();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
