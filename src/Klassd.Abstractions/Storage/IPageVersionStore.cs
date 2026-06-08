using Klassd.Abstractions.Records;

namespace Klassd.Abstractions.Storage;

/// <summary>
/// Persistence for page drafts + published history (the <c>page_versions</c> table/collection),
/// implemented by each DB adapter alongside <see cref="IPageStore"/>. The <c>pages</c> row remains
/// the published snapshot used by delivery; this store holds the one working draft per page and the
/// immutable published versions.
/// </summary>
public interface IPageVersionStore
{
    /// <summary>The single working draft for a page, or null if there is none.</summary>
    Task<PageVersionRecord?> GetDraftAsync(string pageId, CancellationToken ct = default);

    /// <summary>Inserts or replaces the page's draft (one draft per page row).</summary>
    Task SaveDraftAsync(PageVersionRecord draft, CancellationToken ct = default);

    /// <summary>Removes the page's draft, if any.</summary>
    Task DeleteDraftAsync(string pageId, CancellationToken ct = default);

    /// <summary>Published/archived versions for a page, newest first.</summary>
    Task<IReadOnlyList<PageVersionRecord>> GetHistoryAsync(string pageId, CancellationToken ct = default);

    /// <summary>A single version by id (any status), or null.</summary>
    Task<PageVersionRecord?> GetVersionAsync(string versionId, CancellationToken ct = default);

    /// <summary>
    /// Appends an immutable published snapshot. When <paramref name="keepLast"/> &gt; 0, prunes the
    /// oldest published versions so at most that many remain (0 = keep all).
    /// </summary>
    Task AppendPublishedAsync(PageVersionRecord version, int keepLast, CancellationToken ct = default);

    /// <summary>Deletes every version (draft + history) for a page — used when the page is deleted.</summary>
    Task DeleteForPageAsync(string pageId, CancellationToken ct = default);

    /// <summary>Ids of pages in <paramref name="localeCode"/> that currently have a draft (for tree badges).</summary>
    Task<IReadOnlyList<string>> GetDraftPageIdsAsync(string localeCode, CancellationToken ct = default);
}
