using Klassd.Abstractions.Events;
using Klassd.Abstractions.Notifications;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Klassd.Backoffice.Modules.Pages.Models;

namespace Klassd.Backoffice.Modules.Pages.Services;

/// <summary>
/// Engine-side page logic over the storage abstraction. Slug uniqueness, cascade slug renames and
/// translation grouping live here; persistence primitives live in <see cref="IPageStore"/>.
///
/// <para><b>Versioning (when an <see cref="IPageVersionStore"/> is registered):</b> the <c>pages</c>
/// row is the published snapshot delivery serves; edits go to a separate draft (<see cref="SaveDraftAsync"/>)
/// and only reach the row on <see cref="PublishAsync"/>. New pages are draft-first (not delivered until
/// published). With no version store, the service falls back to legacy immediate-publish writes.</para>
/// </summary>
public class PageService(
    IPageStore store,
    IUnitOfWork unitOfWork,
    ICmsEventPublisher? events = null,
    IPageVersionStore? versions = null,
    CmsOptions? options = null,
    ICmsNotifier? notifier = null)
{
    private readonly ICmsEventPublisher _events = events ?? NullCmsEventPublisher.Instance;
    private readonly ICmsNotifier _notifier = notifier ?? NullCmsNotifier.Instance;
    private int KeepLast => options?.VersionHistoryLimit ?? 20;

    /// <summary>Raises a cancelable "before" notification; throws if a handler canceled it.</summary>
    private async Task RaiseBeforeAsync<T>(T notification) where T : ICancelableNotification
    {
        if (!await _notifier.PublishAsync(notification))
            throw new NotificationCanceledException(notification.CancelReason ?? "Operation canceled by a handler.");
    }

    private Task RaiseAsync(string eventType, PageRecord page) =>
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

    // ── Reads ─────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<PageRecord>> GetByLocaleAsync(string localeCode) =>
        await store.GetByLocaleAsync(localeCode);

    public async Task<PageRecord?> GetByIdAsync(string id) =>
        await store.GetByIdAsync(id);

    /// <summary>The page at <paramref name="slug"/> in <paramref name="localeCode"/> (slugs are unique per locale), or null.</summary>
    public async Task<PageRecord?> GetBySlugAsync(string localeCode, string slug) =>
        await store.FindBySlugAsync(localeCode, slug, excludeId: null);

    public async Task<IReadOnlyList<PageRecord>> GetByContentIdAsync(string contentId) =>
        await store.GetByContentIdAsync(contentId);

    /// <summary>
    /// The snapshot to show in the editor: the working draft if one exists, otherwise the published
    /// page row. The returned record carries the page's identity with the draft's editable content.
    /// </summary>
    public async Task<PageRecord?> GetForEditAsync(string id)
    {
        var page = await store.GetByIdAsync(id);
        if (page is null || versions is null) return page;
        var draft = await versions.GetDraftAsync(id);
        return draft is null ? page : ApplyContent(page, draft);
    }

    /// <summary>Whether the page has unpublished draft changes (for the editor's state badge).</summary>
    public async Task<bool> HasDraftAsync(string id) =>
        versions is not null && await versions.GetDraftAsync(id) is not null;

    // ── Create (draft-first when versioning is on) ────────────────────
    public async Task<PageRecord> CreateAsync(CreatePageRequest request, string? actor = null)
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
            Published    = versions is null, // versioned ⇒ draft-first; legacy ⇒ live on create
            CreatedAt    = now,
            UpdatedAt    = now,
        };
        await RaiseBeforeAsync(new PageSavingNotification(page)); // handlers may mutate page or cancel
        await store.InsertAsync(page);

        if (versions is not null)
            await versions.SaveDraftAsync(NewDraft(page, page.Name, page.Slug, page.Data, page.BlockAreas,
                page.PublishAt, page.UnpublishAt, actor, now));

        await _notifier.PublishAsync(new PageSavedNotification(page));
        await RaiseAsync(CmsEventTypes.PageCreated, page);
        return page;
    }

    // ── Edit ──────────────────────────────────────────────────────────
    /// <summary>
    /// Saves edits. With versioning on, writes the page's draft and leaves the published row (and
    /// delivery) untouched; without it, writes through to the row (legacy publish-on-save).
    /// </summary>
    public async Task<PageRecord?> SaveDraftAsync(string id, UpdatePageRequest request, string? actor = null)
    {
        var existing = await store.GetByIdAsync(id);
        if (existing is null) return null;

        if (versions is null)
            return await WriteThroughAsync(existing, request); // legacy

        var content = ContentFrom(existing, request);
        await RaiseBeforeAsync(new PageSavingNotification(content)); // handlers may mutate content or cancel
        await versions.SaveDraftAsync(NewDraft(existing, content.Name, content.Slug,
            content.Data, content.BlockAreas, content.PublishAt, content.UnpublishAt, actor, DateTime.UtcNow));
        await _notifier.PublishAsync(new PageSavedNotification(content));
        return ApplyContent(existing, (await versions.GetDraftAsync(id))!);
    }

    /// <summary>Legacy alias retained for callers that publish on save (no version store).</summary>
    public Task<PageRecord?> UpdateAsync(string id, UpdatePageRequest request) => SaveDraftAsync(id, request);

    // ── Publish / unpublish / discard ─────────────────────────────────
    /// <summary>
    /// Promotes the page's draft (or, if none, re-publishes the current row) to the live snapshot:
    /// applies the content, marks it published, cascades a slug rename, records an immutable version,
    /// and clears the draft. Returns the published page, or null if the id is unknown.
    /// </summary>
    public async Task<PageRecord?> PublishAsync(string id, string? actor = null)
    {
        if (versions is null) return await store.GetByIdAsync(id); // nothing to do without versioning

        var page = await store.GetByIdAsync(id);
        if (page is null) return null;

        var draft = await versions.GetDraftAsync(id);
        // Content to publish: the draft if present, else the row's current content (re-publish).
        var name  = draft?.Name ?? page.Name;
        var slug  = draft?.Slug ?? page.Slug;
        var data  = draft?.Data ?? page.Data;
        var blocks = draft?.BlockAreas ?? page.BlockAreas;
        var publishAt = draft is not null ? draft.PublishAt : page.PublishAt;
        var unpublishAt = draft is not null ? draft.UnpublishAt : page.UnpublishAt;

        await EnsureSlugUnique(page.LocaleCode, slug, excludeId: id);
        var oldSlug = page.Slug;

        page.Name = name;
        page.Slug = slug;
        page.Data = data;
        page.BlockAreas = blocks;
        page.PublishAt = publishAt;
        page.UnpublishAt = unpublishAt;
        page.Published = true;
        page.UpdatedAt = DateTime.UtcNow;

        await RaiseBeforeAsync(new PagePublishingNotification(page)); // handlers may mutate page or cancel

        var published = await store.ReplaceAsync(page);
        if (published is null) return null;

        if (oldSlug != slug)
        {
            await using var tx = await unitOfWork.BeginAsync();
            await CascadeSlugUpdateAsync(id, oldSlug, slug, page.LocaleCode);
            await tx.CommitAsync();
        }

        var history = await versions.GetHistoryAsync(id);
        await versions.AppendPublishedAsync(NewPublished(page, history.Count + 1, actor), KeepLast);
        await versions.DeleteDraftAsync(id);

        await _notifier.PublishAsync(new PagePublishedNotification(published));
        await RaiseAsync(CmsEventTypes.PagePublished, published);
        return published;
    }

    /// <summary>Takes the page offline (no longer delivered); history is retained.</summary>
    public async Task<PageRecord?> UnpublishAsync(string id)
    {
        var page = await store.GetByIdAsync(id);
        if (page is null || !page.Published) return page;

        await RaiseBeforeAsync(new PageUnpublishingNotification(page)); // cancel to keep it live

        page.Published = false;
        page.UpdatedAt = DateTime.UtcNow;
        var updated = await store.ReplaceAsync(page);
        if (updated is not null)
        {
            await _notifier.PublishAsync(new PageUnpublishedNotification(updated));
            await RaiseAsync(CmsEventTypes.PageUnpublished, updated);
        }
        return updated;
    }

    /// <summary>Discards the working draft; the editor reverts to the published row.</summary>
    public async Task DiscardDraftAsync(string id)
    {
        if (versions is not null) await versions.DeleteDraftAsync(id);
    }

    /// <summary>Published version history for a page, newest first (empty without versioning).</summary>
    public async Task<IReadOnlyList<PageVersionRecord>> GetHistoryAsync(string id) =>
        versions is null ? [] : await versions.GetHistoryAsync(id);

    /// <summary>
    /// Loads a prior version's content into the page's draft for review (does not publish it — the
    /// user publishes to make it live). Returns the draft-applied page, or null if unknown/mismatched.
    /// </summary>
    public async Task<PageRecord?> RestoreVersionAsync(string id, string versionId, string? actor = null)
    {
        if (versions is null) return null;
        var page = await store.GetByIdAsync(id);
        if (page is null) return null;
        var version = await versions.GetVersionAsync(versionId);
        if (version is null || version.PageId != id) return null;

        await versions.SaveDraftAsync(NewDraft(page, version.Name, version.Slug, version.Data, version.BlockAreas,
            version.PublishAt, version.UnpublishAt, actor, DateTime.UtcNow));
        return ApplyContent(page, (await versions.GetDraftAsync(id))!);
    }

    /// <summary>Ids of pages in the locale that have a pending draft (for tree badges).</summary>
    public async Task<IReadOnlyList<string>> GetDraftPageIdsAsync(string localeCode) =>
        versions is null ? [] : await versions.GetDraftPageIdsAsync(localeCode);

    private async Task<PageRecord?> WriteThroughAsync(PageRecord existing, UpdatePageRequest request)
    {
        await EnsureSlugUnique(existing.LocaleCode, request.Slug, excludeId: existing.Id);
        var oldSlug = existing.Slug;

        existing.Name        = request.Name;
        existing.Slug        = request.Slug;
        existing.Data        = request.Data;
        existing.BlockAreas  = ToBlockAreaInstances(request.BlockAreas);
        existing.PublishAt   = request.PublishAt;
        existing.UnpublishAt = request.UnpublishAt;
        existing.UpdatedAt   = DateTime.UtcNow;

        await RaiseBeforeAsync(new PageSavingNotification(existing));

        var updated = await store.ReplaceAsync(existing);
        if (updated is null) return null;

        if (oldSlug != existing.Slug)
        {
            await using var tx = await unitOfWork.BeginAsync();
            await CascadeSlugUpdateAsync(existing.Id, oldSlug, existing.Slug, existing.LocaleCode);
            await tx.CommitAsync();
        }

        await _notifier.PublishAsync(new PageSavedNotification(updated));
        await RaiseAsync(CmsEventTypes.PageUpdated, updated);
        return updated;
    }

    // ── Delete (cascade versions) ─────────────────────────────────────
    public async Task<bool> DeleteAsync(string id)
    {
        var existing = await store.GetByIdAsync(id); // capture identity for events before it's gone
        if (existing is not null)
            await RaiseBeforeAsync(new PageDeletingNotification(existing)); // cancel to keep it

        var deleted = await store.DeleteAsync(id);
        if (deleted)
        {
            if (versions is not null) await versions.DeleteForPageAsync(id);
            if (existing is not null)
            {
                await _notifier.PublishAsync(new PageDeletedNotification(existing));
                await RaiseAsync(CmsEventTypes.PageDeleted, existing);
            }
        }
        return deleted;
    }

    // ── Slug cascade ──────────────────────────────────────────────────
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

    private async Task EnsureSlugUnique(string localeCode, string slug, string? excludeId)
    {
        var conflict = await store.FindBySlugAsync(localeCode, slug, excludeId);
        if (conflict is not null)
            throw new InvalidOperationException($"Slug '{slug}' already exists for locale '{localeCode}'.");
    }

    // ── Mapping helpers ───────────────────────────────────────────────
    /// <summary>A copy of <paramref name="page"/> with the version's editable content applied (editor view).</summary>
    private static PageRecord ApplyContent(PageRecord page, PageVersionRecord v) => new()
    {
        Id = page.Id, ContentId = page.ContentId, LocaleCode = page.LocaleCode, ParentId = page.ParentId,
        PageTypeName = page.PageTypeName, Published = page.Published, CreatedAt = page.CreatedAt, UpdatedAt = page.UpdatedAt,
        Name = v.Name, Slug = v.Slug, Data = v.Data, BlockAreas = v.BlockAreas,
        PublishAt = v.PublishAt, UnpublishAt = v.UnpublishAt,
    };

    /// <summary>A PageRecord carrying the page's identity with the request's editable content (for save notifications).</summary>
    private static PageRecord ContentFrom(PageRecord existing, UpdatePageRequest request) => new()
    {
        Id = existing.Id, ContentId = existing.ContentId, LocaleCode = existing.LocaleCode, ParentId = existing.ParentId,
        PageTypeName = existing.PageTypeName, Published = existing.Published, CreatedAt = existing.CreatedAt, UpdatedAt = existing.UpdatedAt,
        Name = request.Name, Slug = request.Slug, Data = request.Data, BlockAreas = ToBlockAreaInstances(request.BlockAreas),
        PublishAt = request.PublishAt, UnpublishAt = request.UnpublishAt,
    };

    private static PageVersionRecord NewDraft(
        PageRecord page, string name, string slug, Dictionary<string, string> data,
        Dictionary<string, List<BlockInstanceRecord>> blocks, DateTime? publishAt, DateTime? unpublishAt,
        string? actor, DateTime now) => new()
    {
        VersionId = Guid.NewGuid().ToString(),
        PageId = page.Id, ContentId = page.ContentId, LocaleCode = page.LocaleCode,
        Status = PageVersionStatus.Draft, Number = 0,
        Name = name, Slug = slug, Data = data, BlockAreas = blocks,
        PublishAt = publishAt, UnpublishAt = unpublishAt, CreatedAt = now, CreatedBy = actor,
    };

    private static PageVersionRecord NewPublished(PageRecord page, int number, string? actor) => new()
    {
        VersionId = Guid.NewGuid().ToString(),
        PageId = page.Id, ContentId = page.ContentId, LocaleCode = page.LocaleCode,
        Status = PageVersionStatus.Published, Number = number,
        Name = page.Name, Slug = page.Slug, Data = page.Data, BlockAreas = page.BlockAreas,
        PublishAt = page.PublishAt, UnpublishAt = page.UnpublishAt, CreatedAt = page.UpdatedAt, CreatedBy = actor,
    };

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
