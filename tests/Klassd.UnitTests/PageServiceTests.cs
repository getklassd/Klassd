using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Backoffice.Modules.Pages.Services;

namespace Klassd.UnitTests;

public class PageServiceTests
{
    private static (PageService svc, InMemoryPageStore store) NewService()
    {
        var store = new InMemoryPageStore();
        return (new PageService(store, new NoopUnitOfWork()), store);
    }

    private static CreatePageRequest Create(
        string name, string slug, string? contentId = null, string? parentId = null, string locale = "en") =>
        new("TestHomePage", locale, contentId, parentId, name, slug, new Dictionary<string, string>());

    [Test]
    public async Task CreateAsync_assigns_guid_id_and_new_content_id()
    {
        var (svc, _) = NewService();
        var page = await svc.CreateAsync(Create("Home", "home"));

        await Assert.That(Guid.TryParse(page.Id, out _)).IsTrue();
        await Assert.That(Guid.TryParse(page.ContentId, out _)).IsTrue();
    }

    [Test]
    public async Task CreateAsync_preserves_supplied_content_id()
    {
        var (svc, _) = NewService();
        var page = await svc.CreateAsync(Create("Home", "home", contentId: "shared-1"));
        await Assert.That(page.ContentId).IsEqualTo("shared-1");
    }

    [Test]
    public async Task CreateAsync_duplicate_locale_slug_throws()
    {
        var (svc, _) = NewService();
        await svc.CreateAsync(Create("A", "dup"));

        await Assert.That(async () => await svc.CreateAsync(Create("B", "dup")))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task CreateAsync_same_slug_different_locale_is_allowed()
    {
        var (svc, _) = NewService();
        await svc.CreateAsync(Create("A", "home", locale: "en"));
        var da = await svc.CreateAsync(Create("A", "home", locale: "da"));
        await Assert.That(da.LocaleCode).IsEqualTo("da");
    }

    [Test]
    public async Task UpdateAsync_changes_fields()
    {
        var (svc, _) = NewService();
        var page = await svc.CreateAsync(Create("Home", "home"));

        var updated = await svc.UpdateAsync(page.Id, new UpdatePageRequest(
            "Renamed", "renamed", new Dictionary<string, string> { ["title"] = "Hi" }));

        await Assert.That(updated).IsNotNull();
        await Assert.That(updated!.Name).IsEqualTo("Renamed");
        await Assert.That(updated.Slug).IsEqualTo("renamed");
        await Assert.That(updated.Data["title"]).IsEqualTo("Hi");
    }

    [Test]
    public async Task UpdateAsync_returns_null_for_unknown_id()
    {
        var (svc, _) = NewService();
        var result = await svc.UpdateAsync("missing", new UpdatePageRequest(
            "x", "x", new Dictionary<string, string>()));
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task UpdateAsync_slug_rename_cascades_to_descendants()
    {
        var (svc, store) = NewService();

        var parent = await svc.CreateAsync(Create("Parent", "a"));
        var child = await svc.CreateAsync(Create("Child", "a/b", parentId: parent.Id));
        var grandchild = await svc.CreateAsync(Create("Grandchild", "a/b/c", parentId: child.Id));

        await svc.UpdateAsync(parent.Id, new UpdatePageRequest(
            "Parent", "x", new Dictionary<string, string>()));

        var childAfter = await store.GetByIdAsync(child.Id);
        var grandchildAfter = await store.GetByIdAsync(grandchild.Id);

        await Assert.That(childAfter!.Slug).IsEqualTo("x/b");
        await Assert.That(grandchildAfter!.Slug).IsEqualTo("x/b/c");
    }

    [Test]
    public async Task GetByContentIdAsync_groups_translations()
    {
        var (svc, _) = NewService();
        await svc.CreateAsync(Create("Home EN", "home", contentId: "c-1", locale: "en"));
        await svc.CreateAsync(Create("Home DA", "hjem", contentId: "c-1", locale: "da"));

        var group = await svc.GetByContentIdAsync("c-1");
        await Assert.That(group).Count().IsEqualTo(2);
        await Assert.That(group.Select(p => p.LocaleCode)).Contains("en");
        await Assert.That(group.Select(p => p.LocaleCode)).Contains("da");
    }

    [Test]
    public async Task DeleteAsync_removes_page()
    {
        var (svc, _) = NewService();
        var page = await svc.CreateAsync(Create("Home", "home"));

        await Assert.That(await svc.DeleteAsync(page.Id)).IsTrue();
        await Assert.That(await svc.GetByIdAsync(page.Id)).IsNull();
        await Assert.That(await svc.DeleteAsync(page.Id)).IsFalse();
    }
}
