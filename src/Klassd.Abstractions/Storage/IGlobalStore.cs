using Klassd.Abstractions.Records;

namespace Klassd.Abstractions.Storage;

/// <summary>Singleton-content ("globals") persistence, keyed by (type name, locale).</summary>
public interface IGlobalStore
{
    Task<GlobalRecord?> GetAsync(string typeName, string localeCode, CancellationToken ct = default);
    Task UpsertAsync(GlobalRecord global, CancellationToken ct = default);
}
