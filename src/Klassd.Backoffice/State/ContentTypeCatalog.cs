using Klassd.Core.Models;
using Klassd.Core.Services;

namespace Klassd.Backoffice.State;

/// <summary>Read access to page/block type metadata for the admin UI (mirrors usePageTypes).</summary>
public sealed class ContentTypeCatalog(PageTypeRegistry pageTypes, BlockTypeRegistry blockTypes)
{
    public IReadOnlyList<PageTypeInfo> PageTypes { get; } = pageTypes.GetAll();
    public IReadOnlyList<BlockTypeInfo> BlockTypes { get; } = blockTypes.GetAll();

    public PageTypeInfo? GetPageType(string typeName) =>
        PageTypes.FirstOrDefault(p => p.TypeName == typeName);

    public BlockTypeInfo? GetBlockType(string typeName) =>
        BlockTypes.FirstOrDefault(b => b.TypeName == typeName);

    public string BlockTypeDisplayName(string typeName) =>
        GetBlockType(typeName)?.DisplayName ?? typeName;

    /// <summary>null/non-empty AllowedChildren = children allowed; empty = none.</summary>
    public bool CanHaveChildren(string pageTypeName)
    {
        var pt = GetPageType(pageTypeName);
        return pt is null || pt.AllowedChildren is null || pt.AllowedChildren.Count > 0;
    }

    public IReadOnlyList<string>? AllowedChildren(string pageTypeName) =>
        GetPageType(pageTypeName)?.AllowedChildren;
}
