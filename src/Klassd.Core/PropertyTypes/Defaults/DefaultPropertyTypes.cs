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

/// <summary>A reference to a stored media item (value = media id). Edited by the engine's media picker.</summary>
public sealed class MediaPropertyType : IPropertyType
{
    public string Alias => "media";
    public Type? EditorComponent => null; // engine maps "media" → its MediaPickerEditor
    public IReadOnlyList<Type> ClrTypes => []; // selected explicitly via [CmsField(FieldType="media")]
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
    ];
}
