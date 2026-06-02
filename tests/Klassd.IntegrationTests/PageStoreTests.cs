using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.IntegrationTests;

public class PageStoreTests
{
    private static PageRecord NewPage(
        string locale = "en",
        string slug = "home",
        string? parentId = null,
        string? contentId = null)
    {
        var now = DateTime.UtcNow;
        return new PageRecord
        {
            Id = Guid.NewGuid().ToString(),
            ContentId = contentId ?? Guid.NewGuid().ToString(),
            LocaleCode = locale,
            ParentId = parentId,
            PageTypeName = "Article",
            Name = "Home",
            Slug = slug,
            Data = new Dictionary<string, string> { ["title"] = "Hello world", ["body"] = "Some text" },
            BlockAreas = new Dictionary<string, List<BlockInstanceRecord>>
            {
                ["heroBlocks"] = new()
                {
                    new BlockInstanceRecord
                    {
                        BlockTypeName = "Hero",
                        Data = new Dictionary<string, string> { ["heading"] = "Welcome" },
                    },
                },
            },
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    [Test]
    public async Task Insert_GetById_Replace_Delete_roundtrips_with_jsonb_data()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await using var scope = harness.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageStore>();

        var page = NewPage();
        await store.InsertAsync(page);

        var fetched = await store.GetByIdAsync(page.Id);
        await Assert.That(fetched).IsNotNull();
        await Assert.That(fetched!.Id).IsEqualTo(page.Id);
        await Assert.That(fetched.ContentId).IsEqualTo(page.ContentId);
        await Assert.That(fetched.LocaleCode).IsEqualTo("en");
        await Assert.That(fetched.PageTypeName).IsEqualTo("Article");
        await Assert.That(fetched.Slug).IsEqualTo("home");

        // jsonb-roundtripped Data survives.
        await Assert.That(fetched.Data["title"]).IsEqualTo("Hello world");
        // BlockAreas + nested block field survive.
        await Assert.That(fetched.BlockAreas).Count().IsEqualTo(1);
        var block = fetched.BlockAreas["heroBlocks"].Single();
        await Assert.That(block.BlockTypeName).IsEqualTo("Hero");
        await Assert.That(block.Data["heading"]).IsEqualTo("Welcome");

        // ReplaceAsync updates an existing record.
        fetched.Name = "Renamed";
        fetched.Data["title"] = "Changed";
        var replaced = await store.ReplaceAsync(fetched);
        await Assert.That(replaced).IsNotNull();
        await Assert.That(replaced!.Name).IsEqualTo("Renamed");
        await Assert.That(replaced.Data["title"]).IsEqualTo("Changed");

        // ReplaceAsync returns null for a missing id.
        var missing = await store.ReplaceAsync(NewPage(slug: "ghost"));
        await Assert.That(missing).IsNull();

        // DeleteAsync returns true, then the page is gone.
        var deleted = await store.DeleteAsync(page.Id);
        await Assert.That(deleted).IsTrue();
        await Assert.That(await store.GetByIdAsync(page.Id)).IsNull();
    }

    [Test]
    public async Task GetByLocaleAsync_filters_by_locale()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await using var scope = harness.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageStore>();

        await store.InsertAsync(NewPage(locale: "en", slug: "a"));
        await store.InsertAsync(NewPage(locale: "en", slug: "b"));
        await store.InsertAsync(NewPage(locale: "da", slug: "a"));

        var en = await store.GetByLocaleAsync("en");
        var da = await store.GetByLocaleAsync("da");

        await Assert.That(en).Count().IsEqualTo(2);
        await Assert.That(da).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Duplicate_locale_slug_throws_but_different_locale_allowed()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await using var scope = harness.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageStore>();

        await store.InsertAsync(NewPage(locale: "en", slug: "dup"));

        // Same (locale, slug) -> unique violation mapped to InvalidOperationException.
        await Assert.That(async () => await store.InsertAsync(NewPage(locale: "en", slug: "dup")))
            .Throws<InvalidOperationException>();

        // Same slug, different locale -> allowed.
        await store.InsertAsync(NewPage(locale: "da", slug: "dup"));
        var da = await store.GetByLocaleAsync("da");
        await Assert.That(da).Count().IsEqualTo(1);
    }

    [Test]
    public async Task FindBySlug_and_GetChildren_behave()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await using var scope = harness.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageStore>();

        var parent = NewPage(locale: "en", slug: "parent");
        await store.InsertAsync(parent);
        var child1 = NewPage(locale: "en", slug: "parent/c1", parentId: parent.Id);
        var child2 = NewPage(locale: "en", slug: "parent/c2", parentId: parent.Id);
        await store.InsertAsync(child1);
        await store.InsertAsync(child2);

        // FindBySlugAsync returns the matching page.
        var found = await store.FindBySlugAsync("en", "parent", excludeId: null);
        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Id).IsEqualTo(parent.Id);

        // Excluding that page's own id returns null.
        var excluded = await store.FindBySlugAsync("en", "parent", excludeId: parent.Id);
        await Assert.That(excluded).IsNull();

        // GetChildrenAsync returns children for (parentId, locale).
        var children = await store.GetChildrenAsync(parent.Id, "en");
        await Assert.That(children).Count().IsEqualTo(2);
    }
}
