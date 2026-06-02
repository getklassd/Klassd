namespace Klassd.Core.Models;

public record BlockTypeInfo(string TypeName, string DisplayName, IReadOnlyList<PageFieldInfo> Fields);
