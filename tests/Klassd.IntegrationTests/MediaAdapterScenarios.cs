using Klassd.Abstractions.Media;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>
/// IMediaStore contract scenarios, run against a REAL database (Postgres/Mongo via Testcontainers,
/// SQLite via temp file). Each scenario isolates itself on a shared database with a unique section
/// name + GUID ids, so one container can serve every test in a class.
/// </summary>
internal static class MediaAdapterScenarios
{
    private static string NewSection() => "s" + Guid.NewGuid().ToString("N")[..10];

    private static MediaRecord NewMedia(string section, string fileName) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Section = section,
        Key = Guid.NewGuid().ToString("N") + ".png",
        FileName = fileName,
        ContentType = "image/png",
        Size = 4096,
        Width = 800,
        Height = 600,
        AltText = "a cat",
        FocalPoints = [new MediaFocalPoint { Breakpoint = "mobile", X = 0.25, Y = 0.75 }],
        Data = new() { ["credit"] = "Jane", ["license"] = "CC0" },
        UploadedAt = DateTime.UtcNow,
    };

    /// <summary>Insert → read back (incl. width/height/alt/focal/data round-trip) → update → delete.</summary>
    public static async Task CrudRoundTrip(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IMediaStore>();
        var section = NewSection();

        var media = NewMedia(section, "cat.png");
        await store.InsertAsync(media);

        var fetched = await store.GetAsync(media.Id);
        await Assert.That(fetched).IsNotNull();
        await Assert.That(fetched!.Section).IsEqualTo(section);
        await Assert.That(fetched.Key).IsEqualTo(media.Key);
        await Assert.That(fetched.FileName).IsEqualTo("cat.png");
        await Assert.That(fetched.ContentType).IsEqualTo("image/png");
        await Assert.That(fetched.Size).IsEqualTo(4096L);
        await Assert.That(fetched.Width).IsEqualTo(800);
        await Assert.That(fetched.Height).IsEqualTo(600);
        await Assert.That(fetched.AltText).IsEqualTo("a cat");
        // focal points + open data bag round-trip through JSON/BSON.
        await Assert.That(fetched.FocalPoints.Single().Breakpoint).IsEqualTo("mobile");
        await Assert.That(fetched.FocalPoints.Single().X).IsEqualTo(0.25);
        await Assert.That(fetched.FocalPoints.Single().Y).IsEqualTo(0.75);
        await Assert.That(fetched.Data["credit"]).IsEqualTo("Jane");
        await Assert.That(fetched.Data["license"]).IsEqualTo("CC0");

        // UpdateAsync replaces editable metadata.
        fetched.AltText = "renamed";
        fetched.FocalPoints = [new MediaFocalPoint { Breakpoint = "default", X = 0.1, Y = 0.2 }];
        fetched.Data = new() { ["credit"] = "John" };
        var updated = await store.UpdateAsync(fetched);
        await Assert.That(updated).IsNotNull();
        await Assert.That(updated!.AltText).IsEqualTo("renamed");

        var reFetched = await store.GetAsync(media.Id);
        await Assert.That(reFetched!.AltText).IsEqualTo("renamed");
        await Assert.That(reFetched.FocalPoints.Single().Breakpoint).IsEqualTo("default");
        await Assert.That(reFetched.Data.ContainsKey("license")).IsFalse();

        // UpdateAsync returns null for a missing id.
        var ghost = NewMedia(section, "ghost.png");
        await Assert.That(await store.UpdateAsync(ghost)).IsNull();

        // DeleteAsync returns true, then the record is gone; missing id → false.
        await Assert.That(await store.DeleteAsync(media.Id)).IsTrue();
        await Assert.That(await store.GetAsync(media.Id)).IsNull();
        await Assert.That(await store.DeleteAsync(media.Id)).IsFalse();
    }

    /// <summary>ListAsync returns only the requested section's records.</summary>
    public static async Task ListFiltersBySection(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IMediaStore>();
        var a = NewSection();
        var b = NewSection();

        await store.InsertAsync(NewMedia(a, "1.png"));
        await store.InsertAsync(NewMedia(a, "2.png"));
        await store.InsertAsync(NewMedia(b, "3.png"));

        await Assert.That((await store.ListAsync(a)).Count).IsEqualTo(2);
        await Assert.That((await store.ListAsync(b)).Count).IsEqualTo(1);
        await Assert.That((await store.ListAsync(NewSection())).Count).IsEqualTo(0);
    }
}
