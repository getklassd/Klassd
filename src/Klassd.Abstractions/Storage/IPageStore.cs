using Klassd.Abstractions.Records;

namespace Klassd.Abstractions.Storage;

/// <summary>
/// Page persistence over a single database. Higher-level concerns (slug uniqueness
/// checks, cascade slug renames, translation grouping) live in the engine service
/// layer on top of these primitives.
/// </summary>
public interface IPageStore
{
    Task<IReadOnlyList<PageRecord>> GetByLocaleAsync(string localeCode, CancellationToken ct = default);
    Task<PageRecord?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<PageRecord>> GetByContentIdAsync(string contentId, CancellationToken ct = default);
    Task<IReadOnlyList<PageRecord>> GetChildrenAsync(string parentId, string localeCode, CancellationToken ct = default);

    /// <summary>Finds a page by (locale, slug), optionally excluding one id (for update checks). Null if none.</summary>
    Task<PageRecord?> FindBySlugAsync(string localeCode, string slug, string? excludeId, CancellationToken ct = default);

    Task InsertAsync(PageRecord page, CancellationToken ct = default);

    /// <summary>Replaces an existing page. Returns the stored record, or null if the id was not found.</summary>
    Task<PageRecord?> ReplaceAsync(PageRecord page, CancellationToken ct = default);

    Task UpdateSlugAsync(string id, string slug, DateTime updatedAt, CancellationToken ct = default);

    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
