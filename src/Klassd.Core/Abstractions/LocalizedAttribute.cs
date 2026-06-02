namespace Klassd.Core.Abstractions;

/// <summary>
/// Marks a page property as having separate values per locale.
/// Can also be applied via <see cref="Klassd.Core.Localization.LocalizationOptions"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class LocalizedAttribute : Attribute { }
