namespace Klassd.Abstractions.Storage;

/// <summary>An index on a JSON key inside a JSON column (pages/globals data). Table/JsonColumn are
/// the logical names; SQL adapters use the column as-is, Mongo maps JsonColumn to the BSON element.</summary>
public sealed record JsonFieldIndex(string Table, string JsonColumn, string Key);

/// <summary>An index on a first-class scalar column (media built-ins). SqlColumn = snake_case
/// (SQLite/Postgres); BsonElement = the PascalCase property name (Mongo).</summary>
public sealed record ColumnIndex(string Table, string SqlColumn, string BsonElement);

/// <summary>
/// Engine-computed index plan consumed by every storage adapter's schema initializer (DI singleton).
/// The engine fills it from [Indexable] content fields; an empty instance ⇒ adapters emit only their
/// built-in indexes (the safe default when used without AddKlassd, e.g. isolated tests).
/// </summary>
public sealed class IndexDefinitions
{
    public IReadOnlyList<JsonFieldIndex> JsonIndexes { get; init; } = [];
    public IReadOnlyList<ColumnIndex> ColumnIndexes { get; init; } = [];

    public static readonly IndexDefinitions Empty = new();
}
