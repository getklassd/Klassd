namespace Klassd.Abstractions.Storage;

/// <summary>
/// A small durable key/value store for engine/system settings that must survive restarts and be
/// shared across stateless instances (e.g. Kubernetes replicas) — the <c>settings</c> table/collection.
/// Values are opaque strings (callers serialize/deserialize their own shape, typically JSON).
/// Implemented by each DB adapter (Mongo/Postgres/SQLite).
/// </summary>
public interface ISettingsStore
{
    /// <summary>The stored value for <paramref name="key"/>, or null if absent.</summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Inserts or replaces the value for <paramref name="key"/>.</summary>
    Task SetAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Removes the entry. Returns false if the key did not exist.</summary>
    Task<bool> DeleteAsync(string key, CancellationToken ct = default);
}
