using Klassd.Backoffice;
using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Backoffice.Modules.Pages.Services;
using TUnit.Core;

namespace Klassd.UnitTests;

public class PageVersioningTests
{
    private static (PageService svc, InMemoryPageStore store, InMemoryPageVersionStore versions) New(int keepLast = 20)
    {
        var store = new InMemoryPageStore();
        var versions = new InMemoryPageVersionStore();
        var svc = new PageService(store, new NoopUnitOfWork(), events: null, versions: versions,
            options: new CmsOptions { VersionHistoryLimit = keepLast });
        return (svc, store, versions);
    }

    private static CreatePageRequest Create(string name = "Home", string slug = "home") =>
        new("TestHomePage", "en", null, null, name, slug, new Dictionary<string, string>());

    private static UpdatePageRequest Update(string name, string slug, string? title = null) =>
        new(name, slug, new Dictionary<string, string> { ["title"] = title ?? name });

    [Test]
    public async Task New_page_is_draft_first_and_not_live()
    {
        var (svc, _, versions) = New();
        var page = await svc.CreateAsync(Create());

        await Assert.That(page.Published).IsFalse();                               // not delivered yet
        await Assert.That(PageSchedule.IsLive(page, DateTime.UtcNow)).IsFalse();
        await Assert.That(await versions.GetDraftAsync(page.Id)).IsNotNull();       // has an initial draft
    }

    [Test]
    public async Task SaveDraft_does_not_change_the_published_row()
    {
        var (svc, store, _) = New();
        var page = await svc.CreateAsync(Create());
        await svc.PublishAsync(page.Id);                                            // now live: Name "Home"

        await svc.SaveDraftAsync(page.Id, Update("Renamed", "home"));

        var row = await store.GetByIdAsync(page.Id);
        await Assert.That(row!.Name).IsEqualTo("Home");                             // published row untouched
        await Assert.That(row.Published).IsTrue();

        var forEdit = await svc.GetForEditAsync(page.Id);
        await Assert.That(forEdit!.Name).IsEqualTo("Renamed");                      // editor sees the draft
        await Assert.That(await svc.HasDraftAsync(page.Id)).IsTrue();
    }

    [Test]
    public async Task Publish_promotes_draft_clears_it_and_records_history()
    {
        var (svc, store, versions) = New();
        var page = await svc.CreateAsync(Create());
        await svc.PublishAsync(page.Id);
        await svc.SaveDraftAsync(page.Id, Update("V2", "home"));

        var published = await svc.PublishAsync(page.Id);

        await Assert.That(published!.Name).IsEqualTo("V2");
        await Assert.That(published.Published).IsTrue();
        await Assert.That(await versions.GetDraftAsync(page.Id)).IsNull();          // draft consumed
        await Assert.That((await versions.GetHistoryAsync(page.Id)).Count).IsEqualTo(2); // two publishes
    }

    [Test]
    public async Task Unpublish_takes_the_page_offline()
    {
        var (svc, store, _) = New();
        var page = await svc.CreateAsync(Create());
        await svc.PublishAsync(page.Id);

        await svc.UnpublishAsync(page.Id);

        var row = await store.GetByIdAsync(page.Id);
        await Assert.That(row!.Published).IsFalse();
        await Assert.That(PageSchedule.IsLive(row, DateTime.UtcNow)).IsFalse();
    }

    [Test]
    public async Task DiscardDraft_reverts_editor_to_published()
    {
        var (svc, _, versions) = New();
        var page = await svc.CreateAsync(Create());
        await svc.PublishAsync(page.Id);
        await svc.SaveDraftAsync(page.Id, Update("Draft", "home"));

        await svc.DiscardDraftAsync(page.Id);

        await Assert.That(await versions.GetDraftAsync(page.Id)).IsNull();
        var forEdit = await svc.GetForEditAsync(page.Id);
        await Assert.That(forEdit!.Name).IsEqualTo("Home"); // back to published
    }

    [Test]
    public async Task History_is_pruned_to_the_retention_cap()
    {
        var (svc, _, versions) = New(keepLast: 2);
        var page = await svc.CreateAsync(Create());
        for (var i = 1; i <= 4; i++)
        {
            await svc.SaveDraftAsync(page.Id, Update($"V{i}", "home"));
            await svc.PublishAsync(page.Id);
        }
        await Assert.That((await versions.GetHistoryAsync(page.Id)).Count).IsEqualTo(2); // only last 2 kept
    }

    [Test]
    public async Task Delete_cascades_versions()
    {
        var (svc, _, versions) = New();
        var page = await svc.CreateAsync(Create());
        await svc.PublishAsync(page.Id);

        await svc.DeleteAsync(page.Id);

        await Assert.That(await versions.GetDraftAsync(page.Id)).IsNull();
        await Assert.That((await versions.GetHistoryAsync(page.Id)).Count).IsEqualTo(0);
    }
}
