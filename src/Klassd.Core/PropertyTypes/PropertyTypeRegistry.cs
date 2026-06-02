using System.Reflection;
using Klassd.Core.Abstractions;

namespace Klassd.Core.PropertyTypes;

/// <summary>
/// Resolves CLR properties to a registered <see cref="IPropertyType"/> by alias or CLR type.
/// </summary>
public sealed class PropertyTypeRegistry
{
    public const string FallbackAlias = "text";

    private readonly Dictionary<string, IPropertyType> _byAlias;
    private readonly Dictionary<Type, IPropertyType> _byClrType = new();

    public PropertyTypeRegistry(IEnumerable<IPropertyType> propertyTypes)
    {
        // Last registration wins per alias, so custom/discovered types can override defaults.
        _byAlias = new();
        foreach (var pt in propertyTypes)
        {
            _byAlias[pt.Alias] = pt;
            foreach (var clr in pt.ClrTypes)
                _byClrType[clr] = pt;
        }
    }

    public string Resolve(PropertyInfo property) =>
        ResolveAlias(property.PropertyType, property.GetCustomAttribute<CmsFieldAttribute>()?.FieldType);

    public string ResolveAlias(Type clrType, string? explicitAlias)
    {
        if (explicitAlias is not null && _byAlias.ContainsKey(explicitAlias))
            return explicitAlias;
        if (_byClrType.TryGetValue(clrType, out var pt))
            return pt.Alias;
        return FallbackAlias;
    }

    public IPropertyType? Get(string alias) =>
        _byAlias.TryGetValue(alias, out var pt) ? pt : null;

    /// <summary>The custom editor component type for an alias, or null to use the engine default.</summary>
    public Type? GetEditorComponent(string alias) =>
        _byAlias.TryGetValue(alias, out var pt) ? pt.EditorComponent : null;
}
