using Klassd.Core.Abstractions;

namespace Klassd.Core.PropertyTypes.Defaults;

// Built-in property types. EditorComponent is null: the engine supplies the built-in
// Blazor editor for each alias (Core has no Blazor dependency). Custom property types
// in a consuming app set EditorComponent to their own Razor component type.

public sealed class TextPropertyType : IPropertyType
{
    public string Alias => "text";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [typeof(string)];
}

public sealed class TextAreaPropertyType : IPropertyType
{
    public string Alias => "textarea";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [];
}

public sealed class NumberPropertyType : IPropertyType
{
    public string Alias => "number";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [typeof(int), typeof(long)];
}

public sealed class CheckboxPropertyType : IPropertyType
{
    public string Alias => "checkbox";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [typeof(bool)];
}

public sealed class DateTimePropertyType : IPropertyType
{
    public string Alias => "datetime-local";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [typeof(DateTime)];
}

public sealed class BlocksPropertyType : IPropertyType
{
    public string Alias => "blocks";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [typeof(BlockArea)];
}

/// <summary>Single-line email input (HTML5 validation). Opt in with <c>[CmsField(FieldType="email")]</c>.</summary>
public sealed class EmailPropertyType : IPropertyType
{
    public string Alias => "email";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [];
}

/// <summary>Single-line URL input (HTML5 validation). Opt in with <c>[CmsField(FieldType="url")]</c>.</summary>
public sealed class UrlPropertyType : IPropertyType
{
    public string Alias => "url";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [];
}

/// <summary>Color picker (value = <c>#rrggbb</c>). Opt in with <c>[CmsField(FieldType="color")]</c>.</summary>
public sealed class ColorPropertyType : IPropertyType
{
    public string Alias => "color";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [];
}

/// <summary>Fractional number. Auto-maps <see cref="decimal"/>/<see cref="double"/>/<see cref="float"/>.</summary>
public sealed class DecimalPropertyType : IPropertyType
{
    public string Alias => "decimal";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [typeof(decimal), typeof(double), typeof(float)];
}

/// <summary>Date without a time component. Auto-maps <see cref="DateOnly"/>.</summary>
public sealed class DatePropertyType : IPropertyType
{
    public string Alias => "date";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [typeof(DateOnly)];
}

/// <summary>Time without a date component. Auto-maps <see cref="TimeOnly"/>.</summary>
public sealed class TimePropertyType : IPropertyType
{
    public string Alias => "time";
    public Type? EditorComponent => null;
    public IReadOnlyList<Type> ClrTypes => [typeof(TimeOnly)];
}

/// <summary>A reference to a stored media item (value = media id). Edited by the engine's media picker.</summary>
public sealed class MediaPropertyType : IPropertyType
{
    public string Alias => "media";
    public Type? EditorComponent => null; // engine maps "media" → its MediaPickerEditor
    // Auto-maps a MediaReference property; a string still opts in via [CmsField(FieldType="media")].
    public IReadOnlyList<Type> ClrTypes => [typeof(MediaReference)];
}

/// <summary>
/// A reference to another page (value = target page's ContentId). Edited by the engine's
/// page picker; restrict link targets with <c>[AllowedRelations(...)]</c>.
/// </summary>
public sealed class RelationshipPropertyType : IPropertyType
{
    public string Alias => "relationship";
    public Type? EditorComponent => null; // engine maps "relationship" → its RelationshipEditor
    // Auto-maps a PageReference property; a string still opts in via [CmsField(FieldType="relationship")].
    public IReadOnlyList<Type> ClrTypes => [typeof(PageReference)];
}

public static class DefaultPropertyTypes
{
    public static readonly IReadOnlyList<IPropertyType> All =
    [
        new TextPropertyType(),
        new TextAreaPropertyType(),
        new NumberPropertyType(),
        new CheckboxPropertyType(),
        new DateTimePropertyType(),
        new BlocksPropertyType(),
        new MediaPropertyType(),
        new RelationshipPropertyType(),
        new EmailPropertyType(),
        new UrlPropertyType(),
        new ColorPropertyType(),
        new DecimalPropertyType(),
        new DatePropertyType(),
        new TimePropertyType(),
    ];
}
