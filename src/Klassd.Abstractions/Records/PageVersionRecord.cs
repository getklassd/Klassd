namespace Klassd.Abstractions.Records;

/// <summary>Lifecycle state of a <see cref="PageVersionRecord"/>.</summary>
public enum PageVersionStatus
{
    /// <summary>The single in-progress working copy for a page (at most one per page row).</summary>
    Draft,
    /// <summary>An immutable snapshot that was published (kept for history/rollback).</summary>
    Published,
    /// <summary>A superseded published version retained beyond the current one.</summary>
    Archived,
}

/// <summary>
/// A versioned snapshot of a page's editable content, stored in the <c>page_versions</c>
/// table/collection alongside (but separate from) the <c>pages</c> row. The <c>pages</c> row holds
/// the currently-published snapshot for fast delivery; this type holds the working draft and the
/// immutable published history. Ids are GUID strings.
/// </summary>
public sealed class PageVersionRecord
{
    public string VersionId { get; set; } = string.Empty;

    /// <summary>The owning page (pages.Id). Versions are per page row, so per locale.</summary>
    public string PageId { get; set; } = string.Empty;

    public string ContentId { get; set; } = string.Empty;   // denormalized for queries
    public string LocaleCode { get; set; } = string.Empty;

    public PageVersionStatus Status { get; set; }

    /// <summary>Monotonic display number per page (1, 2, 3…). Assigned when published.</summary>
    public int Number { get; set; }

    // ── Snapshot payload (the editable page fields) ───────────────────
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Dictionary<string, string> Data { get; set; } = new();
    public Dictionary<string, List<BlockInstanceRecord>> BlockAreas { get; set; } = new();
    public DateTime? PublishAt { get; set; }
    public DateTime? UnpublishAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Username/id of the author who saved this version (audit).</summary>
    public string? CreatedBy { get; set; }
}
