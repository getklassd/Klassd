using System.Reflection;
using System.Text;
using Klassd.Core.Abstractions;
using Klassd.Core.Models;
using Klassd.Core.PropertyTypes;

namespace Klassd.Core.Services;

public abstract class ContentTypeRegistry<TBase> where TBase : class
{
    protected readonly Dictionary<string, Type> Types = new();
    private readonly PropertyTypeRegistry _propertyTypes;

    protected ContentTypeRegistry(IEnumerable<Assembly> assemblies, PropertyTypeRegistry propertyTypes)
    {
        _propertyTypes = propertyTypes;
        foreach (var assembly in assemblies)
        {
            var found = assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(TBase)) && !t.IsAbstract);
            foreach (var type in found)
                Types[type.Name] = type;
        }
    }

    public bool Exists(string typeName) => Types.ContainsKey(typeName);

    /// <summary>
    /// A type is localized if it carries [LocalizedPage] (whole page localized) or
    /// any of its properties carries [Localized].
    /// </summary>
    protected static bool IsTypeLocalized(Type type) =>
        type.GetCustomAttribute<LocalizedPageAttribute>() is not null ||
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.GetCustomAttribute<LocalizedAttribute>() is not null);

    /// <summary>
    /// A property is localized if its declaring type carries [LocalizedPage] (all
    /// properties localized) or the property itself carries [Localized].
    /// </summary>
    private static bool IsPropertyLocalized(Type type, PropertyInfo property) =>
        type.GetCustomAttribute<LocalizedPageAttribute>() is not null ||
        property.GetCustomAttribute<LocalizedAttribute>() is not null;

    /// <summary>
    /// null  = AllowedChildrenAttribute absent → all child types permitted.
    /// empty = attribute present with no types → no children permitted.
    /// list  = attribute present with types → only those types permitted.
    /// </summary>
    protected static IReadOnlyList<string>? GetAllowedChildren(Type type)
    {
        var attr = type.GetCustomAttribute<AllowedChildrenAttribute>();
        return attr is null ? null : attr.ChildTypes.Select(t => t.Name).ToArray();
    }

    protected static string? GetDefaultSlug(Type type) =>
        type.GetCustomAttribute<CmsPageAttribute>()?.DefaultSlug;

    protected IReadOnlyList<PageFieldInfo> GetFields(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p =>
            {
                var attr = p.GetCustomAttribute<CmsFieldAttribute>();
                return new PageFieldInfo(
                    Name: char.ToLower(p.Name[0]) + p.Name[1..],
                    DisplayName: attr?.DisplayName ?? ToDisplayName(p.Name),
                    FieldType: _propertyTypes.Resolve(p),
                    IsLocalized: IsPropertyLocalized(type, p));
            })
            .ToList();

    protected static string ToDisplayName(string name)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
                sb.Append(' ');
            sb.Append(name[i]);
        }
        return sb.ToString();
    }
}
