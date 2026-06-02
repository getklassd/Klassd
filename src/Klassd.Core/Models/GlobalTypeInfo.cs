namespace Klassd.Core.Models;

/// <summary>Reflected metadata for a [CmsGlobal] type. <see cref="Fields"/> uses the same
/// <see cref="PageFieldInfo"/> shape as pages — block areas appear as fields with
/// <c>FieldType == "blocks"</c>, so the admin field/block editors are reused unchanged.</summary>
public record GlobalTypeInfo(
    string TypeName,
    string DisplayName,
    bool IsLocalized,
    IReadOnlyList<PageFieldInfo> Fields);
