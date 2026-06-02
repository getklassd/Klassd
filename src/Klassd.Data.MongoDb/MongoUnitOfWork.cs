using Klassd.Abstractions.Storage;
using MongoDB.Driver;

namespace Klassd.Data.MongoDb;

/// <summary>
/// Best-effort atomicity seam. On a replica set / sharded cluster, <see cref="BeginAsync"/>
/// starts a real client session + transaction. On a single-node deployment (which does not
/// support transactions) it degrades to a no-op transaction whose Commit/Dispose do nothing —
/// matching today's non-transactional behavior.
///
/// LIMITATION: the stores (PageStore/UserStore/PreferencesStore) do not accept an
/// <see cref="IClientSessionHandle"/>, so even when a real session is started here it is NOT
/// threaded through to the individual operations — they run on the ambient (sessionless) path.
/// As a result the started session/transaction is not actually used to make the cascade atomic
/// in this phase; it is a structural placeholder. Threading the session through the stores is a
/// follow-up once the engine requires true multi-document atomicity on supported topologies.
/// </summary>
public sealed class MongoUnitOfWork(MongoContext context) : IUnitOfWork
{
    public async Task<IStorageTransaction> BeginAsync(CancellationToken ct = default)
    {
        var client = context.Database.Client;
        IClientSessionHandle? session = null;
        try
        {
            session = await client.StartSessionAsync(cancellationToken: ct);
            session.StartTransaction();
            return new MongoTransaction(session);
        }
        catch (Exception ex) when (ex is NotSupportedException or MongoClientException or MongoCommandException)
        {
            // Transactions unavailable (e.g. standalone server). Fall back to a no-op.
            session?.Dispose();
            return NoOpTransaction.Instance;
        }
    }

    private sealed class MongoTransaction(IClientSessionHandle session) : IStorageTransaction
    {
        private bool _committed;

        public async Task CommitAsync(CancellationToken ct = default)
        {
            await session.CommitTransactionAsync(ct);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_committed && session.IsInTransaction)
                    await session.AbortTransactionAsync();
            }
            finally
            {
                session.Dispose();
            }
        }
    }

    private sealed class NoOpTransaction : IStorageTransaction
    {
        public static readonly NoOpTransaction Instance = new();
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
