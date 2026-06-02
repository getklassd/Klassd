namespace Klassd.Data.Sqlite;

/// <summary>Adapter options. A single <see cref="ConnectionString"/> points at one database file.</summary>
public sealed class SqliteOptions
{
    public string ConnectionString { get; set; } = string.Empty;
}
