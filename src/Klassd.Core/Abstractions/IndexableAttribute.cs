namespace Klassd.Core.Abstractions;

/// <summary>
/// Marks a content property as indexable: the engine generates a database index on its stored JSON
/// key (pages/globals — block properties are searchable but not DB-indexed, as they live nested in
/// block-area JSON arrays) AND adds it to the searchable field set. Search additionally always
/// matches the built-ins (page name/slug, media filename/alt) regardless of this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IndexableAttribute : Attribute { }
