using Klassd.Abstractions.Records;

namespace Klassd.Backoffice.Modules.Pages.Services;

/// <summary>
/// Resolves whether a page is live (deliverable) at a given instant: it must be
/// <see cref="PageRecord.Published"/> AND within its optional <see cref="PageRecord.PublishAt"/>/
/// <see cref="PageRecord.UnpublishAt"/> window (open-ended bounds mean "from the beginning" /
/// "forever"). Used by the headless delivery projection — the admin always sees every page,
/// draft/scheduled or not. Mirrors <see cref="BlockSchedule"/> at the page level.
/// </summary>
public static class PageSchedule
{
    public static bool IsLive(PageRecord page, DateTime nowUtc) =>
        page.Published &&
        (page.PublishAt is null || page.PublishAt <= nowUtc) &&
        (page.UnpublishAt is null || nowUtc < page.UnpublishAt);
}
