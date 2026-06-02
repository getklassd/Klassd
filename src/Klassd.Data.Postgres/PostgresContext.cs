using Npgsql;

namespace Klassd.Data.Postgres;

/// <summary>Exposes the single <see cref="NpgsqlDataSource"/> and opens connections. Scoped.</summary>
public sealed class PostgresContext(INpgsqlDataSourceProvider provider)
{
    public NpgsqlDataSource DataSource => provider.DataSource;

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default) =>
        await DataSource.OpenConnectionAsync(ct);
}
