namespace Klassd.Core.Abstractions;

/// <summary>
/// Base for a "global": a single editable singleton instance of a code-first type (one per locale),
/// delivered at a stable route (<c>/api/globals/{TypeName}</c>). Unlike a page it has no slug, parent
/// or tree position. Declare [CmsField]/[Localized] properties and BlockArea properties exactly like a
/// page; they're reflected the same way and edited with the same admin field/block editors.
/// </summary>
public abstract class GlobalBase { }
