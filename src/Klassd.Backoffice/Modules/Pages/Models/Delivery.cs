using System.Text.Json.Serialization;
using Klassd.Abstractions.Records;

namespace Klassd.Backoffice.Modules.Pages.Models;

/// <summary>A resolved reference (page or media) returned in a delivered page's <c>references</c> map.</summary>
public sealed record ResolvedReference(
    string Type,        // "page" | "media"
    string Id,
    string? Url,        // public page path, or /api/media/{id}
    string? Slug,       // page only
    string? Title,      // page name / media display name
    string? AltText);   // media only

/// <summary>
/// The headless delivery shape for a single page. Mirrors the public <see cref="PageRecord"/> fields
/// and adds an optional <c>references</c> map populated when <c>?depth&gt;0</c> resolves the page's
/// <c>PageReference</c>/<c>MediaReference</c> fields to URLs. With <c>depth=0</c> (default) the map is
/// omitted, so the payload is identical to the previous page shape (backward compatible).
/// </summary>
public sealed record DeliveredPage(
    string Id,
    string ContentId,
    string LocaleCode,
    string? ParentId,
    string PageTypeName,
    string Name,
    string Slug,
    Dictionary<string, string> Data,
    Dictionary<string, List<BlockInstanceRecord>> BlockAreas,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, ResolvedReference>? References)
{
    public static DeliveredPage From(PageRecord p, IReadOnlyDictionary<string, ResolvedReference>? references) =>
        new(p.Id, p.ContentId, p.LocaleCode, p.ParentId, p.PageTypeName, p.Name, p.Slug,
            p.Data, p.BlockAreas, p.CreatedAt, p.UpdatedAt, references);
}
