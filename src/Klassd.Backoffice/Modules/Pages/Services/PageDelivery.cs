using Klassd.Abstractions.Records;

namespace Klassd.Backoffice.Modules.Pages.Services;

/// <summary>
/// Projects a stored <see cref="PageRecord"/> into the shape served to headless consumers (<c>/api</c>):
/// each block area is filtered to the blocks live at request time (see <see cref="BlockSchedule"/>).
/// Returns a NEW record (block instances are reused but never mutated) so a cached page is left intact —
/// scheduling must resolve per request, above the read-through page cache.
/// </summary>
public static class PageDelivery
{
    public static PageRecord Project(PageRecord page, DateTime nowUtc) => new()
    {
        Id = page.Id,
        ContentId = page.ContentId,
        LocaleCode = page.LocaleCode,
        ParentId = page.ParentId,
        PageTypeName = page.PageTypeName,
        Name = page.Name,
        Slug = page.Slug,
        Data = page.Data,
        BlockAreas = page.BlockAreas.ToDictionary(
            area => area.Key,
            area => BlockSchedule.Active(area.Value, nowUtc)),
        CreatedAt = page.CreatedAt,
        UpdatedAt = page.UpdatedAt,
    };

    public static IReadOnlyList<PageRecord> Project(IReadOnlyList<PageRecord> pages, DateTime nowUtc) =>
        pages.Select(p => Project(p, nowUtc)).ToList();

    /// <summary>Like <see cref="Project(IReadOnlyList{PageRecord},DateTime)"/> but drops pages outside their publish window.</summary>
    public static IReadOnlyList<PageRecord> ProjectLive(IReadOnlyList<PageRecord> pages, DateTime nowUtc) =>
        pages.Where(p => PageSchedule.IsLive(p, nowUtc)).Select(p => Project(p, nowUtc)).ToList();
}
