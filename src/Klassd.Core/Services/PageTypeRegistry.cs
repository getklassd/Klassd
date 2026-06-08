using System.Reflection;
using Klassd.Core.Models;
using Klassd.Core.PropertyTypes;

namespace Klassd.Core.Services;

public class PageTypeRegistry(IEnumerable<Assembly> assemblies, PropertyTypeRegistry propertyTypes)
    : ContentTypeRegistry<Abstractions.PageBase>(assemblies, propertyTypes)
{
    public IReadOnlyList<PageTypeInfo> GetAll() =>
        Types.Values.Select(Describe).ToList();

    /// <summary>The described type for <paramref name="typeName"/>, or null if unknown.</summary>
    public PageTypeInfo? Get(string typeName) =>
        Types.TryGetValue(typeName, out var t) ? Describe(t) : null;

    private PageTypeInfo Describe(Type t) =>
        new(t.Name, ToDisplayName(t.Name), IsTypeLocalized(t), GetFields(t),
            GetAllowedChildren(t), GetDefaultSlug(t), GetIcon(t));
}
