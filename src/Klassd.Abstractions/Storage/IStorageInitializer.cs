namespace Klassd.Abstractions.Storage;

/// <summary>
/// One-time storage setup (schema/tables/indexes). The engine runs all registered
/// initializers at startup BEFORE seeding and before serving traffic — so adapters
/// can rely on their schema existing. Implementations must be idempotent.
/// </summary>
public interface IStorageInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
