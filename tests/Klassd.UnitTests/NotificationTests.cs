using Klassd.Abstractions.Notifications;
using Klassd.Backoffice;
using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Backoffice.Modules.Pages.Services;
using Microsoft.Extensions.DependencyInjection;
using TUnit.Core;

namespace Klassd.UnitTests;

public class NotificationTests
{
    private sealed class StampOnSaving : INotificationHandler<PageSavingNotification>
    {
        public Task HandleAsync(PageSavingNotification n, CancellationToken ct = default)
        {
            n.Page.Data["audited"] = "yes"; // mutate the entity in-flight
            return Task.CompletedTask;
        }
    }

    private sealed class CancelPublishing : INotificationHandler<PagePublishingNotification>
    {
        public Task HandleAsync(PagePublishingNotification n, CancellationToken ct = default)
        {
            n.Cancel = true;
            n.CancelReason = "Not allowed";
            return Task.CompletedTask;
        }
    }

    private sealed class CancelDeleting : INotificationHandler<PageDeletingNotification>
    {
        public Task HandleAsync(PageDeletingNotification n, CancellationToken ct = default) { n.Cancel = true; return Task.CompletedTask; }
    }

    private sealed class CountSaved : INotificationHandler<PageSavedNotification>
    {
        public int Count { get; private set; }
        public Task HandleAsync(PageSavedNotification n, CancellationToken ct = default) { Count++; return Task.CompletedTask; }
    }

    private static (PageService svc, InMemoryPageStore store) New(Action<ServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        var provider = services.BuildServiceProvider();
        var store = new InMemoryPageStore();
        var svc = new PageService(store, new NoopUnitOfWork(), versions: new InMemoryPageVersionStore(),
            options: new CmsOptions(), notifier: new CmsNotifier(provider));
        return (svc, store);
    }

    private static CreatePageRequest Create(string name = "Home", string slug = "home") =>
        new("TestHomePage", "en", null, null, name, slug, new Dictionary<string, string>());

    [Test]
    public async Task Saving_handler_can_mutate_the_entity()
    {
        var (svc, _) = New(s => s.AddSingleton<INotificationHandler<PageSavingNotification>, StampOnSaving>());
        var page = await svc.CreateAsync(Create());
        await Assert.That(page.Data.GetValueOrDefault("audited")).IsEqualTo("yes");
    }

    [Test]
    public async Task Publishing_handler_can_cancel()
    {
        var (svc, _) = New(s => s.AddSingleton<INotificationHandler<PagePublishingNotification>, CancelPublishing>());
        var page = await svc.CreateAsync(Create());

        await Assert.That(async () => await svc.PublishAsync(page.Id)).Throws<NotificationCanceledException>();
        // Publish aborted before completing — the draft was not consumed.
        await Assert.That(await svc.HasDraftAsync(page.Id)).IsTrue();
    }

    [Test]
    public async Task Deleting_handler_can_cancel()
    {
        var (svc, store) = New(s => s.AddSingleton<INotificationHandler<PageDeletingNotification>, CancelDeleting>());
        var page = await svc.CreateAsync(Create());

        await Assert.That(async () => await svc.DeleteAsync(page.Id)).Throws<NotificationCanceledException>();
        await Assert.That(await store.GetByIdAsync(page.Id)).IsNotNull(); // still there
    }

    [Test]
    public async Task After_saved_notification_fires_on_create_and_edit()
    {
        var counter = new CountSaved();
        var (svc, _) = New(s => s.AddSingleton<INotificationHandler<PageSavedNotification>>(counter));
        var page = await svc.CreateAsync(Create());
        await svc.SaveDraftAsync(page.Id, new UpdatePageRequest("Renamed", "home", new Dictionary<string, string>()));

        await Assert.That(counter.Count).IsEqualTo(2); // create + draft save
    }

    [Test]
    public async Task No_handlers_means_no_interference()
    {
        var (svc, _) = New(_ => { });
        var page = await svc.CreateAsync(Create());
        var published = await svc.PublishAsync(page.Id);
        await Assert.That(published!.Published).IsTrue();
    }
}
