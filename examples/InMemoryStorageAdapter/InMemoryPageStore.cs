using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;

namespace Klassd.Examples.InMemoryStorage;

/// <summary>
/// <see cref="IPageStore"/> over <see cref="InMemoryDatabase.Pages"/>. These are storage primitives
/// only — slug-uniqueness enforcement, cascade renames and translation grouping live in the engine's
/// service layer on top of them.
/// </summary>
public sealed class InMemoryPageStore(InMemoryDatabase db) : IPageStore
{
    public Task<IReadOnlyList<PageRecord>> GetByLocaleAsync(string localeCode, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PageRecord>>(
            db.Pages.Values.Where(p => p.LocaleCode == localeCode).Select(p => p.Clone()).ToList());

    public Task<PageRecord?> GetByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(db.Pages.TryGetValue(id, out var p) ? p.Clone() : null);

    public Task<IReadOnlyList<PageRecord>> GetByContentIdAsync(string contentId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PageRecord>>(
            db.Pages.Values.Where(p => p.ContentId == contentId).Select(p => p.Clone()).ToList());

    public Task<IReadOnlyList<PageRecord>> GetChildrenAsync(string parentId, string localeCode, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<PageRecord>>(
            db.Pages.Values.Where(p => p.ParentId == parentId && p.LocaleCode == localeCode).Select(p => p.Clone()).ToList());

    public Task<PageRecord?> FindBySlugAsync(string localeCode, string slug, string? excludeId, CancellationToken ct = default)
    {
        var match = db.Pages.Values.FirstOrDefault(p =>
            p.LocaleCode == localeCode && p.Slug == slug && p.Id != excludeId);
        return Task.FromResult(match?.Clone());
    }

    public Task InsertAsync(PageRecord page, CancellationToken ct = default)
    {
        db.Pages[page.Id] = page.Clone();   // clone on write so later caller mutations don't leak in
        return Task.CompletedTask;
    }

    public Task<PageRecord?> ReplaceAsync(PageRecord page, CancellationToken ct = default)
    {
        if (!db.Pages.ContainsKey(page.Id))
            return Task.FromResult<PageRecord?>(null);   // unknown id → null, per contract

        var stored = page.Clone();
        db.Pages[page.Id] = stored;
        return Task.FromResult<PageRecord?>(stored.Clone());
    }

    public Task UpdateSlugAsync(string id, string slug, DateTime updatedAt, CancellationToken ct = default)
    {
        if (db.Pages.TryGetValue(id, out var p))
        {
            p.Slug = slug;
            p.UpdatedAt = updatedAt;
        }
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(db.Pages.TryRemove(id, out _));
}
