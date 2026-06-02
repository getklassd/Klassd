using Klassd.Abstractions.Storage;
using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Backoffice.Modules.Pages.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.IntegrationTests;

public class PageServiceIntegrationTests
{
    private static CreatePageRequest Create(
        string slug,
        string? parentId = null,
        string locale = "en",
        string? contentId = null) =>
        new(
            PageTypeName: "Article",
            LocaleCode: locale,
            ContentId: contentId,
            ParentId: parentId,
            Name: slug,
            Slug: slug,
            Data: new Dictionary<string, string> { ["title"] = slug },
            BlockAreas: null);

    [Test]
    public async Task Cascade_slug_rename_persists_in_sqlite()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await using var scope = harness.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageStore>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>(); // real SqliteUnitOfWork
        var service = new PageService(store, uow);

        var parent = await service.CreateAsync(Create("a"));
        var child = await service.CreateAsync(Create("a/b", parentId: parent.Id));
        var grandchild = await service.CreateAsync(Create("a/b/c", parentId: child.Id));

        // Rename the parent slug a -> x; descendants must cascade.
        var updated = await service.UpdateAsync(parent.Id, new UpdatePageRequest(
            Name: "x", Slug: "x", Data: parent.Data, BlockAreas: null));
        await Assert.That(updated).IsNotNull();
        await Assert.That(updated!.Slug).IsEqualTo("x");

        // Reload from the real SQLite store and assert the cascade persisted.
        var reloadedChild = await store.GetByIdAsync(child.Id);
        var reloadedGrandchild = await store.GetByIdAsync(grandchild.Id);
        await Assert.That(reloadedChild).IsNotNull();
        await Assert.That(reloadedChild!.Slug).IsEqualTo("x/b");
        await Assert.That(reloadedGrandchild).IsNotNull();
        await Assert.That(reloadedGrandchild!.Slug).IsEqualTo("x/b/c");
    }

    [Test]
    public async Task Duplicate_slug_create_throws()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await using var scope = harness.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageStore>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>(); // real SqliteUnitOfWork
        var service = new PageService(store, uow);

        await service.CreateAsync(Create("dupe"));

        await Assert.That(async () => await service.CreateAsync(Create("dupe")))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetByContentId_groups_translations()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await using var scope = harness.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageStore>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>(); // real SqliteUnitOfWork
        var service = new PageService(store, uow);

        var contentId = Guid.NewGuid().ToString();
        await service.CreateAsync(Create("home", locale: "en", contentId: contentId));
        await service.CreateAsync(Create("hjem", locale: "da", contentId: contentId));

        var translations = await service.GetByContentIdAsync(contentId);
        await Assert.That(translations).Count().IsEqualTo(2);

        var locales = translations.Select(p => p.LocaleCode).OrderBy(l => l).ToList();
        await Assert.That(locales[0]).IsEqualTo("da");
        await Assert.That(locales[1]).IsEqualTo("en");
    }
}
