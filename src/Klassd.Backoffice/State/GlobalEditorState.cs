using Klassd.Backoffice.Modules.Globals.Services;
using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Backoffice.Modules.Pages.Services;   // ContentTypeCatalog, MarketTime
using Klassd.Core.Localization;
using Klassd.Core.Models;
using Klassd.Core.Services;

namespace Klassd.Backoffice.State;

/// <summary>
/// Editor state for a single global (per type + locale). Reuses the page editor's field/block
/// components via <see cref="IBlockAreaHost"/>; has no slug/parent/tree concerns.
/// </summary>
public sealed class GlobalEditorState(
    GlobalService globals, GlobalTypeRegistry registry, ContentTypeCatalog catalog,
    LocaleState locale, ToastService toasts, LocaleRegistry locales) : IBlockAreaHost
{
    public event Action? Changed;
    private void Notify() => Changed?.Invoke();

    public GlobalTypeInfo? Current { get; private set; }
    public string TypeName => Current?.TypeName ?? "";
    public bool Loaded { get; private set; }

    public Dictionary<string, string> FormData { get; private set; } = new();
    public Dictionary<string, List<BlockData>> BlockAreas { get; private set; } = new();

    public IReadOnlyList<PageFieldInfo> TextFields =>
        Current?.Fields.Where(f => f.FieldType != "blocks").ToList() ?? [];
    public IReadOnlyList<PageFieldInfo> BlockFields =>
        Current?.Fields.Where(f => f.FieldType == "blocks").ToList() ?? [];

    // ── Load / save ───────────────────────────────────────────────────
    public async Task LoadAsync(string typeName, string localeCode)
    {
        Current = registry.Get(typeName);
        ResetBlockForm();
        Loaded = false;
        if (Current is null) { FormData = new(); BlockAreas = new(); Notify(); return; }

        var content = await globals.GetForEditAsync(typeName, localeCode);
        FormData = content.Data;
        BlockAreas = content.BlockAreas;
        Loaded = true;
        Notify();
    }

    public async Task<bool> SaveAsync(string localeCode)
    {
        if (Current is null) return false;
        try
        {
            await globals.SaveAsync(TypeName, localeCode, FormData, BlockAreas);
            toasts.Success($"{Current.DisplayName} saved");
            return true;
        }
        catch (InvalidOperationException ex) { toasts.Error(ex.Message); return false; }
        catch (Exception) { toasts.Error("Save failed."); return false; }
    }

    // ── IBlockAreaHost (block editing) — same flow as EditorPanelState, over BlockAreas ──
    public string? ActiveBlockArea { get; private set; }
    public int? EditingBlockIndex { get; private set; }
    public bool AddBlockOpen { get; private set; }
    public string NewBlockType { get; set; } = "";
    public Dictionary<string, string> NewBlockData { get; private set; } = new();
    public bool NewBlockTypeDisabled { get; private set; }
    public DateTime? NewBlockStartUtc { get; set; }
    public DateTime? NewBlockEndUtc { get; set; }
    public int NewBlockPriority { get; set; }

    // Globals have no schedule-preview UI; badges evaluate at "now".
    public DateTime? PreviewAtUtc => null;
    public DateTime PreviewInstant => DateTime.UtcNow;

    public TimeZoneInfo MarketTimeZone => locales.TimeZoneFor(locale.SelectedLocale);
    public string MarketTimeZoneLabel => MarketTime.Label(MarketTimeZone, DateTime.UtcNow);

    public DateTime? NewBlockStartLocal
    {
        get => NewBlockStartUtc is { } u ? MarketTime.ToLocal(u, MarketTimeZone) : null;
        set => NewBlockStartUtc = value is { } v ? MarketTime.ToUtc(v, MarketTimeZone) : null;
    }
    public DateTime? NewBlockEndLocal
    {
        get => NewBlockEndUtc is { } u ? MarketTime.ToLocal(u, MarketTimeZone) : null;
        set => NewBlockEndUtc = value is { } v ? MarketTime.ToUtc(v, MarketTimeZone) : null;
    }

    public BlockTypeInfo? NewBlockTypeDef => catalog.GetBlockType(NewBlockType);

    public string ActiveBlockAreaLabel =>
        BlockFields.FirstOrDefault(f => f.Name == ActiveBlockArea)?.DisplayName ?? ActiveBlockArea ?? "";

    public IReadOnlyList<BlockData> BlocksIn(string areaName) =>
        BlockAreas.TryGetValue(areaName, out var list) ? list : [];

    public void ShowAddBlock(string areaName)
    {
        ActiveBlockArea = areaName;
        EditingBlockIndex = null;
        ResetNewBlock();
        AddBlockOpen = true;
        Notify();
    }

    public void HideAddBlock()
    {
        AddBlockOpen = false;
        ActiveBlockArea = null;
        EditingBlockIndex = null;
        ResetNewBlock();
        Notify();
    }

    public void OnNewBlockTypeChanged()
    {
        if (NewBlockTypeDisabled) return;
        NewBlockData = NewBlockTypeDef?.Fields.ToDictionary(f => f.Name, _ => "") ?? new();
        Notify();
    }

    public void EditBlock(string areaName, int index)
    {
        var block = BlockAreas[areaName][index];
        ActiveBlockArea = areaName;
        EditingBlockIndex = index;
        NewBlockType = block.BlockTypeName;
        NewBlockData = new Dictionary<string, string>(block.Data);
        NewBlockStartUtc = block.StartUtc;
        NewBlockEndUtc = block.EndUtc;
        NewBlockPriority = block.Priority;
        NewBlockTypeDisabled = true;
        AddBlockOpen = true;
        Notify();
    }

    public void CommitBlock()
    {
        if (string.IsNullOrEmpty(NewBlockType) || ActiveBlockArea is null) return;
        var entry = new BlockData(NewBlockType, new Dictionary<string, string>(NewBlockData),
            NewBlockStartUtc, NewBlockEndUtc, NewBlockPriority);
        var list = BlockAreas.TryGetValue(ActiveBlockArea, out var existing) ? existing : BlockAreas[ActiveBlockArea] = [];
        if (EditingBlockIndex is { } i) list[i] = entry;
        else list.Add(entry);
        HideAddBlock();
    }

    public void RemoveBlock(string areaName, int index)
    {
        if (BlockAreas.TryGetValue(areaName, out var list) && index < list.Count)
            list.RemoveAt(index);
        Notify();
    }

    private void ResetBlockForm()
    {
        AddBlockOpen = false;
        ActiveBlockArea = null;
        EditingBlockIndex = null;
        ResetNewBlock();
    }

    private void ResetNewBlock()
    {
        NewBlockType = "";
        NewBlockData = new();
        NewBlockTypeDisabled = false;
        NewBlockStartUtc = NewBlockEndUtc = null;
        NewBlockPriority = 0;
    }
}
