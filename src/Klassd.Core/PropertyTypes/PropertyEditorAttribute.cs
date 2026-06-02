using System.Reflection;

namespace Klassd.Core.PropertyTypes;

/// <summary>
/// Marks a Blazor editor component as a CMS property type. Put it on the editor
/// component itself — no separate <see cref="IPropertyType"/> class and no
/// <c>AddPropertyType</c> call needed; the engine auto-discovers it by assembly scan.
/// <code>
/// @attribute [PropertyEditor("color")]
/// @inherits PropertyEditorBase
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PropertyEditorAttribute(string alias, params Type[] clrTypes) : Attribute
{
    public string Alias { get; } = alias;

    /// <summary>CLR types this editor is the default for (optional; usually selected via [CmsField]).</summary>
    public IReadOnlyList<Type> ClrTypes { get; } = clrTypes;
}

/// <summary>An <see cref="IPropertyType"/> synthesized from a <see cref="PropertyEditorAttribute"/>-marked component.</summary>
public sealed class ComponentPropertyType(string alias, Type editorComponent, IReadOnlyList<Type> clrTypes) : IPropertyType
{
    public string Alias => alias;
    public Type? EditorComponent => editorComponent;
    public IReadOnlyList<Type> ClrTypes => clrTypes;
}

public static class PropertyEditorDiscovery
{
    /// <summary>Scans assemblies for components marked with <see cref="PropertyEditorAttribute"/>.</summary>
    public static IEnumerable<IPropertyType> Discover(IEnumerable<Assembly> assemblies) =>
        assemblies
            .SelectMany(SafeGetTypes)
            .Select(t => (Type: t, Attr: t.GetCustomAttribute<PropertyEditorAttribute>()))
            .Where(x => x.Attr is not null)
            .Select(x => (IPropertyType)new ComponentPropertyType(x.Attr!.Alias, x.Type, x.Attr.ClrTypes));

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }
}
