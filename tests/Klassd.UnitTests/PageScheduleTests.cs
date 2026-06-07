using Klassd.Abstractions.Records;
using Klassd.Backoffice.Modules.Pages.Services;
using TUnit.Core;

namespace Klassd.UnitTests;

public class PageScheduleTests
{
    private static readonly DateTime Now = new(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc);

    private static PageRecord Page(DateTime? publishAt = null, DateTime? unpublishAt = null) =>
        new()
        {
            Id = "1", ContentId = "c1", LocaleCode = "en", PageTypeName = "ContentPage",
            Name = "Home", Slug = "home", PublishAt = publishAt, UnpublishAt = unpublishAt,
        };

    [Test]
    public async Task Open_ended_window_is_live()
    {
        await Assert.That(PageSchedule.IsLive(Page(), Now)).IsTrue();
    }

    [Test]
    public async Task Before_publish_at_is_not_live()
    {
        await Assert.That(PageSchedule.IsLive(Page(publishAt: Now.AddHours(1)), Now)).IsFalse();
    }

    [Test]
    public async Task At_or_after_publish_at_is_live()
    {
        await Assert.That(PageSchedule.IsLive(Page(publishAt: Now), Now)).IsTrue();
        await Assert.That(PageSchedule.IsLive(Page(publishAt: Now.AddHours(-1)), Now)).IsTrue();
    }

    [Test]
    public async Task At_or_after_unpublish_at_is_not_live()
    {
        await Assert.That(PageSchedule.IsLive(Page(unpublishAt: Now), Now)).IsFalse();
        await Assert.That(PageSchedule.IsLive(Page(unpublishAt: Now.AddHours(-1)), Now)).IsFalse();
    }

    [Test]
    public async Task Inside_window_is_live()
    {
        var page = Page(publishAt: Now.AddHours(-1), unpublishAt: Now.AddHours(1));
        await Assert.That(PageSchedule.IsLive(page, Now)).IsTrue();
    }

    [Test]
    public async Task ProjectLive_drops_pages_outside_their_window()
    {
        var live = Page();
        live.Id = "live";
        var future = Page(publishAt: Now.AddHours(1));
        future.Id = "future";
        var expired = Page(unpublishAt: Now.AddHours(-1));
        expired.Id = "expired";

        var result = PageDelivery.ProjectLive([live, future, expired], Now);

        await Assert.That(result.Select(p => p.Id)).IsEquivalentTo(["live"]);
    }
}
