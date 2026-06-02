using System.Reflection;
using Klassd.Core.Models;
using Klassd.Core.PropertyTypes;

namespace Klassd.Core.Services;

public class PageTypeRegistry(IEnumerable<Assembly> assemblies, PropertyTypeRegistry propertyTypes)
    : ContentTypeRegistry<Abstractions.PageBase>(assemblies, propertyTypes)
{
    public IReadOnlyList<PageTypeInfo> GetAll() =>
        Types.Values
            .Select(t => new PageTypeInfo(
                t.Name, ToDisplayName(t.Name), IsTypeLocalized(t), GetFields(t),
                GetAllowedChildren(t), GetDefaultSlug(t)))
            .ToList();
}
