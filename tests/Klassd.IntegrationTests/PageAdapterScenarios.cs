using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Backoffice.Modules.Pages.Services;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;

namespace Klassd.IntegrationTests;

/// <summary>
/// Storage-adapter contract scenarios, run against a REAL database (Postgres/Mongo via
/// Testcontainers). Each scenario isolates itself on a shared database by using a unique
/// locale code + GUID ids, so a single container can serve all tests in a class.
/// </summary>
internal static class PageAdapterScenarios
{
    private static string NewLocale() => "t" + Guid.NewGuid().ToString("N")[..10];
    private static string NewId() => Guid.NewGuid().ToString();

    private static PageRecord NewPage(string locale, string slug, string? parentId = null, string? contentId = null) => new()
    {
        Id = NewId(),
        ContentId = contentId ?? NewId(),
        LocaleCode = locale,
        ParentId = parentId,
        PageTypeName = "ContentPage",
        Name = slug,
        Slug = slug,
        Data = new() { ["title"] = "T-" + slug },
        BlockAreas = new()
        {
            ["body"] = [new BlockInstanceRecord
            {
                BlockTypeName = "TextBlock",
                Data = new() { ["content"] = "hello" },
                StartUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                Priority = 7,
            }],
        },
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    /// <summary>Insert → read back (incl. jsonb/bson Data + BlockAreas round-trip) → replace → delete.</summary>
    public static async Task CrudRoundTrip(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageStore>();
        var locale = NewLocale();

        var page = NewPage(locale, "home");
        await store.InsertAsync(page);

        var fetched = await store.GetByIdAsync(page.Id);
        await Assert.That(fetched).IsNotNull();
        await Assert.That(fetched!.Name).IsEqualTo("home");
        await Assert.That(fetched.Data["title"]).IsEqualTo("T-home");           // dict round-trip
        await Assert.That(fetched.BlockAreas["body"][0].BlockTypeName).IsEqualTo("TextBlock");
        await Assert.That(fetched.BlockAreas["body"][0].Data["content"]).IsEqualTo("hello"); // nested round-trip
        // Block schedule round-trips inside the block_areas JSON/BSON (no schema change).
        var scheduled = fetched.BlockAreas["body"][0];
        await Assert.That(scheduled.Priority).IsEqualTo(7);
        await Assert.That(scheduled.StartUtc!.Value).IsEqualTo(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await Assert.That(scheduled.EndUtc!.Value).IsEqualTo(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        fetched.Name = "renamed";
        var replaced = await store.ReplaceAsync(fetched);
        await Assert.That(replaced!.Name).IsEqualTo("renamed");

        await Assert.That(await store.DeleteAsync(page.Id)).IsTrue();
        await Assert.That(await store.GetByIdAsync(page.Id)).IsNull();
    }

    /// <summary>GetByLocale filters by locale; FindBySlug + GetChildren behave.</summary>
    public static async Task QueriesAndChildren(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageStore>();
        var locale = NewLocale();

        var parent = NewPage(locale, "p");
        var child = NewPage(locale, "p/c", parentId: parent.Id);
        await store.InsertAsync(parent);
        await store.InsertAsync(child);

        var inLocale = await store.GetByLocaleAsync(locale);
        await Assert.That(inLocale.Count).IsEqualTo(2);
        await Assert.That((await store.GetByLocaleAsync(NewLocale())).Count).IsEqualTo(0);

        await Assert.That((await store.FindBySlugAsync(locale, "p", null))!.Id).IsEqualTo(parent.Id);
        await Assert.That(await store.FindBySlugAsync(locale, "p", excludeId: parent.Id)).IsNull();

        var children = await store.GetChildrenAsync(parent.Id, locale);
        await Assert.That(children.Count).IsEqualTo(1);
        await Assert.That(children[0].Id).IsEqualTo(child.Id);
    }

    /// <summary>Engine-level duplicate (locale, slug) → InvalidOperationException (EnsureSlugUnique).</summary>
    public static async Task DuplicateSlugThrows(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var service = Service(scope);
        var locale = NewLocale();

        await service.CreateAsync(Create(locale, "dupe"));
        await Assert.That(async () => await service.CreateAsync(Create(locale, "dupe")))
            .Throws<InvalidOperationException>();
    }

    /// <summary>Cascade slug rename persists to the real store (parent a → x ⇒ x/b, x/b/c).</summary>
    public static async Task CascadeRenamePersists(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageStore>();
        var service = Service(scope);
        var locale = NewLocale();

        var parent = await service.CreateAsync(Create(locale, "a"));
        var child = await service.CreateAsync(Create(locale, "a/b", parent.Id));
        var grandchild = await service.CreateAsync(Create(locale, "a/b/c", child.Id));

        var updated = await service.UpdateAsync(parent.Id, new UpdatePageRequest("x", "x", parent.Data, null));
        await Assert.That(updated!.Slug).IsEqualTo("x");

        await Assert.That((await store.GetByIdAsync(child.Id))!.Slug).IsEqualTo("x/b");
        await Assert.That((await store.GetByIdAsync(grandchild.Id))!.Slug).IsEqualTo("x/b/c");
    }

    /// <summary>Translations sharing a ContentId across locales are grouped.</summary>
    public static async Task TranslationGrouping(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var service = Service(scope);
        var contentId = NewId();
        var l1 = NewLocale();
        var l2 = NewLocale();

        await service.CreateAsync(Create(l1, "home", contentId: contentId));
        await service.CreateAsync(Create(l2, "hjem", contentId: contentId));

        var group = await service.GetByContentIdAsync(contentId);
        await Assert.That(group.Count).IsEqualTo(2);
    }

    /// <summary>Preferences round-trip. (Users moved to the external Klassd.Auth store.)</summary>
    public static async Task UsersAndPreferences(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var prefs = scope.ServiceProvider.GetRequiredService<IPreferencesStore>();

        var userId = NewId();

        var pref = new UserPreferencesRecord { UserId = userId, SelectedLocale = "en", Collapsed = ["a", "b"] };
        await prefs.UpsertAsync(pref);
        var got = await prefs.GetAsync(userId);
        await Assert.That(got!.SelectedLocale).IsEqualTo("en");
        await Assert.That(got.Collapsed.Count).IsEqualTo(2);

        pref.SelectedLocale = "da";
        await prefs.UpsertAsync(pref);
        await Assert.That((await prefs.GetAsync(userId))!.SelectedLocale).IsEqualTo("da");
    }

    private static PageService Service(AsyncServiceScope scope) =>
        new(scope.ServiceProvider.GetRequiredService<IPageStore>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());

    private static CreatePageRequest Create(string locale, string slug, string? parentId = null, string? contentId = null) =>
        new("ContentPage", locale, contentId, parentId, slug, slug, new() { ["title"] = slug }, null);
}
