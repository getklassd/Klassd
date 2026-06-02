using Microsoft.Data.Sqlite;

namespace Klassd.Data.Sqlite;

/// <summary>
/// Opens connections for the configured connection string. Scoped.
///
/// Microsoft.Data.Sqlite has no shared data-source object like Npgsql; instead a new
/// <see cref="SqliteConnection"/> is created per operation. The provider pools file
/// connections by default, so opening per call is cheap.
/// </summary>
public sealed class SqliteContext(SqliteOptions options)
{
    public string ConnectionString => options.ConnectionString;

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = new SqliteConnection(options.ConnectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}
