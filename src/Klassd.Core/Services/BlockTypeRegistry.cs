using System.Reflection;
using Klassd.Core.Abstractions;
using Klassd.Core.Models;
using Klassd.Core.PropertyTypes;

namespace Klassd.Core.Services;

public class BlockTypeRegistry(IEnumerable<Assembly> assemblies, PropertyTypeRegistry propertyTypes)
    : ContentTypeRegistry<BlockBase>(assemblies, propertyTypes)
{
    public IReadOnlyList<BlockTypeInfo> GetAll() =>
        Types.Values
            .Select(t => new BlockTypeInfo(t.Name, ToDisplayName(t.Name), GetFields(t)))
            .ToList();
}
