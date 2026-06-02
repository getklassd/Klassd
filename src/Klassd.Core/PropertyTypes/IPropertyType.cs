namespace Klassd.Core.PropertyTypes;

/// <summary>
/// An extensible property (field) type. Maps CLR types to an editor and an alias.
/// </summary>
public interface IPropertyType
{
    /// <summary>Stable key used as <c>FieldType</c> in metadata / editor lookup (e.g. "text").</summary>
    string Alias { get; }

    /// <summary>
    /// Optional Blazor editor component type (a <c>ComponentBase</c>) used to edit this
    /// property in the admin. <c>null</c> = use the engine's built-in editor for this alias.
    /// A consumer's custom property type points this at its own Razor component
    /// (e.g. <c>typeof(ColorEditor)</c>) — no JS, no registration. Kept as
    /// <see cref="Type"/> so Core stays free of any Blazor dependency.
    /// </summary>
    Type? EditorComponent { get; }

    /// <summary>CLR types this property type is the default mapping for. May be empty.</summary>
    IReadOnlyList<Type> ClrTypes { get; }
}
