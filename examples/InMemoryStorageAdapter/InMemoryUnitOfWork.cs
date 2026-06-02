using Klassd.Abstractions.Storage;

namespace Klassd.Examples.InMemoryStorage;

/// <summary>
/// <see cref="IUnitOfWork"/> — the atomicity seam for multi-record operations (e.g. cascade slug
/// renames). A relational adapter maps this to a real transaction; here we provide a best-effort
/// no-op, the same fallback the Mongo adapter uses on single-node deployments. Single-key writes
/// are already atomic via the underlying <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>;
/// cross-key rollback is not provided (acceptable for an in-memory example).
/// </summary>
public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task<IStorageTransaction> BeginAsync(CancellationToken ct = default) =>
        Task.FromResult<IStorageTransaction>(new NoOpTransaction());

    private sealed class NoOpTransaction : IStorageTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
