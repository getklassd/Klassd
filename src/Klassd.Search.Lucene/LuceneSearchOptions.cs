namespace Klassd.Search.Lucene;

/// <summary>Configuration for the Lucene full-text search index.</summary>
public sealed class LuceneSearchOptions
{
    /// <summary>
    /// Filesystem path for the on-disk index. Null/empty ⇒ in-memory (lost on restart, but the
    /// startup rebuild re-seeds it from the database). On Kubernetes an in-memory or pod-local index
    /// is rebuilt from the shared DB on each pod start; cross-pod live sync is a separate concern
    /// (a pluggable coordinator) — single-pod and rebuild-on-start work with no extra infrastructure.
    /// </summary>
    public string? IndexPath { get; set; }

    /// <summary>Force a full rebuild from the database on startup even when the index is non-empty.</summary>
    public bool RebuildOnStartup { get; set; }
}
