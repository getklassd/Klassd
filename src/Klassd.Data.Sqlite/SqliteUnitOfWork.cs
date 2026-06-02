using Klassd.Abstractions.Storage;

namespace Klassd.Data.Sqlite;

/// <summary>
/// No-op unit of work. The stores in this adapter each open their own connection per
/// call and do NOT enlist in a shared transaction, so a real <c>BEGIN</c> here would
/// hold a write lock that those separate connections then deadlock against (SQLite is
/// single-writer) — e.g. the engine's cascade slug rename. Since nothing enlists, the
/// transaction provides no atomicity anyway, so we make it a no-op: the cascade runs as
/// individual UPDATE statements (best-effort), matching the Mongo adapter on deployments
/// without multi-document transactions. Full store-enlistment is a deliberate follow-up.
/// </summary>
public sealed class SqliteUnitOfWork : IUnitOfWork
{
    public Task<IStorageTransaction> BeginAsync(CancellationToken ct = default) =>
        Task.FromResult<IStorageTransaction>(new NoOpTransaction());

    private sealed class NoOpTransaction : IStorageTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
