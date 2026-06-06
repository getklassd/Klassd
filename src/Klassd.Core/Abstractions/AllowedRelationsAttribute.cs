namespace Klassd.Core.Abstractions;

/// <summary>
/// Restricts which page types a relationship field may link to. Put it on a
/// <see cref="PageReference"/> property (or any field resolved to the "relationship" type).
/// <para>
/// Absent, or present with no types = any page type may be linked. Present with types =
/// only those page types appear in the picker. (Unlike <see cref="AllowedChildrenAttribute"/>,
/// an empty list does NOT mean "none" — a relationship with no valid targets is meaningless.)
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AllowedRelationsAttribute : Attribute
{
    public Type[] PageTypes { get; }
    public AllowedRelationsAttribute(params Type[] pageTypes) => PageTypes = pageTypes;
}
