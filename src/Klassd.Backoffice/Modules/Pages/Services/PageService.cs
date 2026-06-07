using Klassd.Abstractions.Events;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Klassd.Backoffice.Modules.Pages.Models;

namespace Klassd.Backoffice.Modules.Pages.Services;

/// <summary>
/// Engine-side page logic over the storage abstraction. Slug uniqueness,
/// cascade slug renames and translation grouping live here; persistence
/// primitives live in <see cref="IPageStore"/>.
/// </summary>
public class PageService(IPageStore store, IUnitOfWork unitOfWork, ICmsEventPublisher? events = null)
{
    private readonly ICmsEventPublisher _events = events ?? NullCmsEventPublisher.Instance;

    private Task PublishAsync(string eventType, PageRecord page) =>
        _events.PublishAsync(new CmsEvent
        {
            EventType    = eventType,
            ResourceKind = "page",
            Id           = page.Id,
            ContentId    = page.ContentId,
            LocaleCode   = page.LocaleCode,
            Slug         = page.Slug,
            TypeName     = page.PageTypeName,
        });
    public async Task<IReadOnlyList<PageRecord>> GetByLocaleAsync(string localeCode) =>
        await store.GetByLocaleAsync(localeCode);

    public async Task<PageRecord?> GetByIdAsync(string id) =>
        await store.GetByIdAsync(id);

    /// <summary>The page at <paramref name="slug"/> in <paramref name="localeCode"/> (slugs are unique per locale), or null.</summary>
    public async Task<PageRecord?> GetBySlugAsync(string localeCode, string slug) =>
        await store.FindBySlugAsync(localeCode, slug, excludeId: null);

    public async Task<IReadOnlyList<PageRecord>> GetByContentIdAsync(string contentId) =>
        await store.GetByContentIdAsync(contentId);

    public async Task<PageRecord> CreateAsync(CreatePageRequest request)
    {
        await EnsureSlugUnique(request.LocaleCode, request.Slug, excludeId: null);

        var now = DateTime.UtcNow;
        var page = new PageRecord
        {
            Id           = Guid.NewGuid().ToString(),
            ContentId    = string.IsNullOrEmpty(request.ContentId) ? Guid.NewGuid().ToString() : request.ContentId,
            LocaleCode   = request.LocaleCode,
            ParentId     = request.ParentId,
            PageTypeName = request.PageTypeName,
            Name         = request.Name,
            Slug         = request.Slug,
            Data         = request.Data,
            BlockAreas   = ToBlockAreaInstances(request.BlockAreas),
            PublishAt    = request.PublishAt,
            UnpublishAt  = request.UnpublishAt,
            CreatedAt    = now,
            UpdatedAt    = now,
        };
        await store.InsertAsync(page);
        await PublishAsync(CmsEventTypes.PageCreated, page);
        return page;
    }

    public async Task<PageRecord?> UpdateAsync(string id, UpdatePageRequest request)
    {
        var existing = await store.GetByIdAsync(id);
        if (existing is null) return null;

        await EnsureSlugUnique(existing.LocaleCode, request.Slug, excludeId: id);

        var oldSlug = existing.Slug;

        existing.Name        = request.Name;
        existing.Slug        = request.Slug;
        existing.Data        = request.Data;
        existing.BlockAreas  = ToBlockAreaInstances(request.BlockAreas);
        existing.PublishAt   = request.PublishAt;
        existing.UnpublishAt = request.UnpublishAt;
        existing.UpdatedAt   = DateTime.UtcNow;

        var updated = await store.ReplaceAsync(existing);
        if (updated is null) return null;

        if (oldSlug != request.Slug)
        {
            await using var tx = await unitOfWork.BeginAsync();
            await CascadeSlugUpdateAsync(id, oldSlug, request.Slug, existing.LocaleCode);
            await tx.CommitAsync();
        }

        await PublishAsync(CmsEventTypes.PageUpdated, updated);
        return updated;
    }

    private async Task CascadeSlugUpdateAsync(string pageId, string oldSlug, string newSlug, string localeCode)
    {
        var children = await store.GetChildrenAsync(pageId, localeCode);
        foreach (var child in children)
        {
            var oldChildSlug = child.Slug;
            var segment = ExtractSegment(child.Slug, oldSlug);
            var newChildSlug = CombineSlugs(newSlug, segment);
            await store.UpdateSlugAsync(child.Id, newChildSlug, DateTime.UtcNow);
            await CascadeSlugUpdateAsync(child.Id, oldChildSlug, newChildSlug, localeCode);
        }
    }

    private static string ExtractSegment(string childSlug, string parentSlug) =>
        parentSlug.Length > 0 && childSlug.StartsWith(parentSlug + "/")
            ? childSlug[(parentSlug.Length + 1)..] : childSlug;

    private static string CombineSlugs(string parentSlug, string segment) =>
        parentSlug.Length > 0 ? $"{parentSlug}/{segment}" : segment;

    public async Task<bool> DeleteAsync(string id)
    {
        var existing = await store.GetByIdAsync(id); // capture identity for the event before it's gone
        var deleted = await store.DeleteAsync(id);
        if (deleted && existing is not null)
            await PublishAsync(CmsEventTypes.PageDeleted, existing);
        return deleted;
    }

    private async Task EnsureSlugUnique(string localeCode, string slug, string? excludeId)
    {
        var conflict = await store.FindBySlugAsync(localeCode, slug, excludeId);
        if (conflict is not null)
            throw new InvalidOperationException($"Slug '{slug}' already exists for locale '{localeCode}'.");
    }

    private static Dictionary<string, List<BlockInstanceRecord>> ToBlockAreaInstances(
        Dictionary<string, List<BlockData>>? areas) =>
        areas?.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(b => new BlockInstanceRecord
            {
                BlockTypeName = b.BlockTypeName,
                Data = b.Data,
                StartUtc = b.StartUtc,
                EndUtc = b.EndUtc,
                Priority = b.Priority,
            }).ToList()
        ) ?? new();
}
