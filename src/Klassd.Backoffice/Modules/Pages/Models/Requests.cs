namespace Klassd.Backoffice.Modules.Pages.Models;

public record BlockData(
    string BlockTypeName,
    Dictionary<string, string> Data,
    DateTime? StartUtc = null,
    DateTime? EndUtc = null,
    int Priority = 0);

public record CreatePageRequest(
    string PageTypeName,
    string LocaleCode,
    string? ContentId,
    string? ParentId,
    string Name,
    string Slug,
    Dictionary<string, string> Data,
    Dictionary<string, List<BlockData>>? BlockAreas = null,
    DateTime? PublishAt = null,
    DateTime? UnpublishAt = null);

public record UpdatePageRequest(
    string Name,
    string Slug,
    Dictionary<string, string> Data,
    Dictionary<string, List<BlockData>>? BlockAreas = null,
    DateTime? PublishAt = null,
    DateTime? UnpublishAt = null);
