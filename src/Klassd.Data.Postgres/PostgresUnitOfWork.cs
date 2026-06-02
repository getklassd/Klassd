using Klassd.Abstractions.Storage;
using Npgsql;

namespace Klassd.Data.Postgres;

/// <summary>
/// Transaction primitive over the data source. <see cref="BeginAsync"/> opens a dedicated
/// connection and begins a transaction.
///
/// NOTE: the stores in this adapter each open their own connection per call, so they do
/// NOT automatically enlist in this transaction. This UoW is a correct standalone
/// transaction primitive; full store-enlistment (sharing this connection/transaction)
/// is a deliberate follow-up. The engine's cascade slug rename still functions via the
/// individual UPDATE statements — best-effort, matching the Mongo adapter's behavior on
/// deployments without multi-document transactions.
/// </summary>
public sealed class PostgresUnitOfWork(PostgresContext context) : IUnitOfWork
{
    public async Task<IStorageTransaction> BeginAsync(CancellationToken ct = default)
    {
        var conn = await context.OpenConnectionAsync(ct);
        try
        {
            var tx = await conn.BeginTransactionAsync(ct);
            return new PostgresStorageTransaction(conn, tx);
        }
        catch
        {
            await conn.DisposeAsync();
            throw;
        }
    }

    private sealed class PostgresStorageTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
        : IStorageTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => transaction.CommitAsync(ct);

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
