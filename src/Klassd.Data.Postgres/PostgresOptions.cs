namespace Klassd.Data.Postgres;

/// <summary>Adapter options. A single <see cref="ConnectionString"/> points at one database.</summary>
public sealed class PostgresOptions
{
    public string ConnectionString { get; set; } = string.Empty;
}
