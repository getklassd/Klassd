namespace Klassd.Abstractions.Storage;

/// <summary>
/// Atomicity seam for multi-document operations (e.g. cascade slug renames).
/// Postgres maps this to a transaction; Mongo to a session (with a best-effort
/// no-op fallback on single-node deployments that lack transactions).
/// </summary>
public interface IUnitOfWork
{
    Task<IStorageTransaction> BeginAsync(CancellationToken ct = default);
}

public interface IStorageTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
}
