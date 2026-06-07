using Klassd.Abstractions.Records;

namespace Klassd.Backoffice.Modules.Pages.Services;

/// <summary>
/// Resolves whether a page is live (publishable) at a given instant. A page is live when
/// <paramref name="nowUtc"/> falls within its optional <see cref="PageRecord.PublishAt"/>/
/// <see cref="PageRecord.UnpublishAt"/> window; open-ended bounds mean "from the beginning" /
/// "forever". Used by the headless delivery projection — the admin always sees every page,
/// scheduled or not. Mirrors <see cref="BlockSchedule"/> at the page level.
/// </summary>
public static class PageSchedule
{
    public static bool IsLive(PageRecord page, DateTime nowUtc) =>
        (page.PublishAt is null || page.PublishAt <= nowUtc) &&
        (page.UnpublishAt is null || nowUtc < page.UnpublishAt);
}
