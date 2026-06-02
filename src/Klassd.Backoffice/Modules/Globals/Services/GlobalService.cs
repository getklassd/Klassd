using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Core.Localization;
using Klassd.Core.Models;
using Klassd.Core.Services;

namespace Klassd.Backoffice.Modules.Globals.Services;

/// <summary>Editable content of a global for the admin editor (block areas as the editor's BlockData).</summary>
public sealed record GlobalContent(
    Dictionary<string, string> Data,
    Dictionary<string, List<BlockData>> BlockAreas);

/// <summary>
/// Orchestrates globals: reflected type list (from <see cref="GlobalTypeRegistry"/>), per-(type,locale)
/// persistence (<see cref="IGlobalStore"/>), and locale-fallback resolution for delivery — mirroring how
/// pages work, reusing the block-area record/instance shapes.
/// </summary>
public sealed class GlobalService(IGlobalStore store, GlobalTypeRegistry registry, LocaleRegistry locales)
{
    public IReadOnlyList<GlobalTypeInfo> ListTypes() => registry.GetAll();

    public GlobalTypeInfo? GetType(string typeName) => registry.Get(typeName);

    /// <summary>Admin load: the exact (type, locale) content for editing (no fallback). Empty when unset.</summary>
    public async Task<GlobalContent> GetForEditAsync(string typeName, string localeCode, CancellationToken ct = default)
    {
        var rec = registry.Exists(typeName) ? await store.GetAsync(typeName, localeCode, ct) : null;
        if (rec is null) return new GlobalContent(new(), new());
        return new GlobalContent(
            new Dictionary<string, string>(rec.Data),
            rec.BlockAreas.ToDictionary(a => a.Key, a => a.Value.Select(ToBlockData).ToList()));
    }

    /// <summary>Delivery: resolve a global by type name + locale, walking the locale fallback chain
    /// (same chain pages/dictionary use). Returns an empty record for a known type with no stored
    /// content; null only when the type name is unknown.</summary>
    public async Task<GlobalRecord?> GetForDeliveryAsync(string typeName, string localeCode, CancellationToken ct = default)
    {
        if (!registry.Exists(typeName)) return null;
        foreach (var code in locales.GetFallbackChain(localeCode))
            if (await store.GetAsync(typeName, code, ct) is { } hit)
                return hit;
        return new GlobalRecord { TypeName = typeName, LocaleCode = localeCode };
    }

    public async Task SaveAsync(string typeName, string localeCode,
        Dictionary<string, string> data, Dictionary<string, List<BlockData>>? blockAreas, CancellationToken ct = default)
    {
        if (!registry.Exists(typeName))
            throw new InvalidOperationException($"Unknown global type '{typeName}'.");

        await store.UpsertAsync(new GlobalRecord
        {
            TypeName = typeName,
            LocaleCode = localeCode,
            Data = data,
            BlockAreas = (blockAreas ?? new()).ToDictionary(a => a.Key, a => a.Value.Select(ToInstance).ToList()),
            UpdatedAt = DateTime.UtcNow,
        }, ct);
    }

    private static BlockData ToBlockData(BlockInstanceRecord b) =>
        new(b.BlockTypeName, new Dictionary<string, string>(b.Data), b.StartUtc, b.EndUtc, b.Priority);

    private static BlockInstanceRecord ToInstance(BlockData b) =>
        new() { BlockTypeName = b.BlockTypeName, Data = b.Data, StartUtc = b.StartUtc, EndUtc = b.EndUtc, Priority = b.Priority };
}
