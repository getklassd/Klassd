using Klassd.Abstractions.Media;
using Klassd.Abstractions.Records;
using Klassd.Core.Localization;
using Klassd.Core.PropertyTypes;
using Klassd.Core.PropertyTypes.Defaults;
using Klassd.Core.Services;
using Klassd.Backoffice.Modules.Pages.Services;
using TUnit.Core;

namespace Klassd.UnitTests;

public class ReferenceResolverTests
{
    private sealed class FakeMediaStore(MediaRecord one) : IMediaStore
    {
        public Task<MediaRecord?> GetAsync(string id, CancellationToken ct = default) =>
            Task.FromResult<MediaRecord?>(one.Id == id ? one : null);
        public Task<IReadOnlyList<MediaRecord>> ListAsync(string section, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MediaRecord>>([]);
        public Task InsertAsync(MediaRecord media, CancellationToken ct = default) => Task.CompletedTask;
        public Task<MediaRecord?> UpdateAsync(MediaRecord media, CancellationToken ct = default) => Task.FromResult<MediaRecord?>(null);
        public Task<bool> DeleteAsync(string id, CancellationToken ct = default) => Task.FromResult(false);
    }

    private static async Task<ReferenceResolver> NewResolver()
    {
        var store = new InMemoryPageStore();
        await store.InsertAsync(new PageRecord
        {
            Id = "tgt", ContentId = "target-c", LocaleCode = "en",
            PageTypeName = "TestHomePage", Name = "Target Page", Slug = "target",
        });
        var media = new FakeMediaStore(new MediaRecord
        {
            Id = "media-1", Section = "images", FileName = "pic.jpg", DisplayName = "Hero", AltText = "alt text",
        });
        var propertyTypes = new PropertyTypeRegistry(DefaultPropertyTypes.All);
        var pageTypes = new PageTypeRegistry([typeof(TestRelationPage).Assembly], propertyTypes);
        var locales = new LocaleRegistry([new LocaleDefinition("en", Mandatory: true)]);
        return new ReferenceResolver(store, media, pageTypes, locales);
    }

    private static PageRecord Source() => new()
    {
        Id = "src", ContentId = "src-c", LocaleCode = "en", PageTypeName = "TestRelationPage",
        Name = "Source", Slug = "source",
        Data = new() { ["related"] = "target-c", ["picture"] = "media-1" },
    };

    [Test]
    public async Task Depth_zero_resolves_nothing()
    {
        var resolver = await NewResolver();
        var result = await resolver.ResolveAsync(Source(), depth: 0, expand: null);
        await Assert.That(result.References).IsNull();
    }

    [Test]
    public async Task Resolves_page_reference_to_url_and_slug()
    {
        var resolver = await NewResolver();
        var result = await resolver.ResolveAsync(Source(), depth: 1, expand: null);

        await Assert.That(result.References).IsNotNull();
        var page = result.References!["related"];
        await Assert.That(page.Type).IsEqualTo("page");
        await Assert.That(page.Url).IsEqualTo("/target");
        await Assert.That(page.Slug).IsEqualTo("target");
        await Assert.That(page.Title).IsEqualTo("Target Page");
    }

    [Test]
    public async Task Resolves_media_reference_to_url()
    {
        var resolver = await NewResolver();
        var result = await resolver.ResolveAsync(Source(), depth: 1, expand: null);

        var media = result.References!["picture"];
        await Assert.That(media.Type).IsEqualTo("media");
        await Assert.That(media.Url).IsEqualTo("/api/media/media-1");
        await Assert.That(media.Title).IsEqualTo("Hero");
        await Assert.That(media.AltText).IsEqualTo("alt text");
    }

    [Test]
    public async Task Expand_limits_which_fields_resolve()
    {
        var resolver = await NewResolver();
        var result = await resolver.ResolveAsync(Source(), depth: 1, expand: new HashSet<string> { "picture" });

        await Assert.That(result.References!.ContainsKey("picture")).IsTrue();
        await Assert.That(result.References!.ContainsKey("related")).IsFalse();
    }

    [Test]
    public async Task Missing_target_is_skipped()
    {
        var resolver = await NewResolver();
        var src = Source();
        src.Data["related"] = "does-not-exist";
        var result = await resolver.ResolveAsync(src, depth: 1, expand: null);

        await Assert.That(result.References!.ContainsKey("related")).IsFalse();
        await Assert.That(result.References!.ContainsKey("picture")).IsTrue(); // the media one still resolves
    }
}
