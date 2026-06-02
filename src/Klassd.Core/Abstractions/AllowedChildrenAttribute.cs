namespace Klassd.Core.Abstractions;

[AttributeUsage(AttributeTargets.Class)]
public sealed class AllowedChildrenAttribute : Attribute
{
    public Type[] ChildTypes { get; }
    public AllowedChildrenAttribute(params Type[] childTypes) => ChildTypes = childTypes;
}
