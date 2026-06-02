namespace Klassd.Data.MongoDb;

/// <summary>
/// Adapter options. One instance per app: a single connection string and a single
/// target database.
/// </summary>
public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "klassd";
}
