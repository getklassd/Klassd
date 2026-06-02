using Npgsql;

namespace Klassd.Data.Postgres;

/// <summary>
/// Owns the single <see cref="NpgsqlDataSource"/> for the configured connection string.
/// A data source owns its connection pool and is thread-safe, so it is shared (singleton).
/// </summary>
public interface INpgsqlDataSourceProvider
{
    NpgsqlDataSource DataSource { get; }
}

public sealed class NpgsqlDataSourceProvider(PostgresOptions options)
    : INpgsqlDataSourceProvider, IAsyncDisposable, IDisposable
{
    private readonly Lazy<NpgsqlDataSource> _source =
        new(() => NpgsqlDataSource.Create(options.ConnectionString));

    public NpgsqlDataSource DataSource => _source.Value;

    public void Dispose()
    {
        if (_source.IsValueCreated)
            _source.Value.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_source.IsValueCreated)
            await _source.Value.DisposeAsync();
    }
}
