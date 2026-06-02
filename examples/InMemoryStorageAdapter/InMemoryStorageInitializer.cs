using Klassd.Abstractions.Storage;

namespace Klassd.Examples.InMemoryStorage;

/// <summary>
/// <see cref="IStorageInitializer"/> runs once at startup, before seeding and before traffic, so an
/// adapter can create its schema/tables/indexes. The dictionaries already exist for an in-memory
/// backend, so there is nothing to do — but the seam must still be implemented (and be idempotent).
/// </summary>
public sealed class InMemoryStorageInitializer : IStorageInitializer
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
