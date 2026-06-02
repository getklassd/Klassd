using System.Reflection;
using Klassd.Core.Abstractions;
using Klassd.Core.Models;
using Klassd.Core.PropertyTypes;

namespace Klassd.Core.Services;

/// <summary>Discovers [CmsGlobal] types (GlobalBase subclasses) and exposes their reflected
/// field/block metadata, reusing the same reflection pages use.</summary>
public class GlobalTypeRegistry(IEnumerable<Assembly> assemblies, PropertyTypeRegistry propertyTypes)
    : ContentTypeRegistry<GlobalBase>(assemblies, propertyTypes)
{
    public IReadOnlyList<GlobalTypeInfo> GetAll() => Types.Values.Select(Describe).ToList();

    public GlobalTypeInfo? Get(string typeName) =>
        Types.TryGetValue(typeName, out var t) ? Describe(t) : null;

    private GlobalTypeInfo Describe(Type t) => new(
        TypeName: t.Name,
        DisplayName: t.GetCustomAttribute<CmsGlobalAttribute>()?.DisplayName ?? ToDisplayName(t.Name),
        IsLocalized: IsTypeLocalized(t),
        Fields: GetFields(t));   // same reflection as pages — block areas come through as "blocks" fields
}
