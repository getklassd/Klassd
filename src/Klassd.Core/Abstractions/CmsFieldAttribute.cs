namespace Klassd.Core.Abstractions;

[AttributeUsage(AttributeTargets.Property)]
public sealed class CmsFieldAttribute : Attribute
{
    public string? DisplayName { get; set; }
    public string? FieldType { get; set; }
}
