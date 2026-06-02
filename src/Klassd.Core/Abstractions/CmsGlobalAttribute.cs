namespace Klassd.Core.Abstractions;

/// <summary>Optional metadata for a global type. A [CmsGlobal]-derived type (i.e. a
/// <see cref="GlobalBase"/> subclass) is discovered by base class; this attribute only customizes
/// the admin display label. The delivery name is always the CLR type name.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CmsGlobalAttribute : Attribute
{
    /// <summary>Admin display label. Null = humanized type name ("Site Header").</summary>
    public string? DisplayName { get; set; }
}
