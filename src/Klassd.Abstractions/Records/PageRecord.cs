namespace Klassd.Abstractions.Records;

/// <summary>
/// DB-agnostic representation of a page. Adapters map this to/from their own
/// persistence model — no Bson/Npgsql concerns leak into this type.
/// Ids are GUID strings end-to-end.
/// </summary>
public sealed class PageRecord
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Groups translations of the same content across locales.</summary>
    public string ContentId { get; set; } = string.Empty;
    public string LocaleCode { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string PageTypeName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public Dictionary<string, string> Data { get; set; } = new();

    /// <summary>Named block areas keyed by camelCase property name (e.g. "heroBlocks").</summary>
    public Dictionary<string, List<BlockInstanceRecord>> BlockAreas { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── Draft / published state ───────────────────────────────────────
    // The pages row IS the currently-published snapshot. A page is delivered only when Published
    // is true (and inside its publish window). New pages are created draft-first (Published=false)
    // and go live on the first Publish; edits go to a separate draft version, leaving this snapshot
    // (and delivery) untouched until republished. Pre-versioning rows migrate to Published=true.
    public bool Published { get; set; }

    // ── Optional publish window (page-level scheduling) ───────────────
    // Headless delivery (/api) serves a page only while it is live; the admin always sees it.
    // Null bounds are open-ended: no PublishAt = live immediately, no UnpublishAt = never expires.
    // Compared in UTC. Distinct from per-block scheduling on BlockInstanceRecord.
    public DateTime? PublishAt { get; set; }
    public DateTime? UnpublishAt { get; set; }
}

public sealed class BlockInstanceRecord
{
    public string BlockTypeName { get; set; } = string.Empty;
    public Dictionary<string, string> Data { get; set; } = new();

    // ── Optional scheduling (publish window) ──────────────────────────
    // Headless delivery (/api) returns only blocks active at request time; the admin always
    // sees every block. Null bounds are open-ended: no StartUtc = live immediately, no EndUtc =
    // never expires (an always-on fallback). Compared in UTC.
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }

    /// <summary>Precedence among simultaneously-active blocks (higher wins); ties keep authored order. Default 0.</summary>
    public int Priority { get; set; }
}
