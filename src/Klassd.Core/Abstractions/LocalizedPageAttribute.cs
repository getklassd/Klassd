namespace Klassd.Core.Abstractions;

/// <summary>
/// Marks a page type as localized. The slug becomes per-locale and all
/// properties marked with [Localized] (or configured via LocalizationOptions)
/// will have separate values per locale.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class LocalizedPageAttribute : Attribute { }
