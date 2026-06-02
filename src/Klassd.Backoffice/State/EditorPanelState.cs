using System.Text;
using Klassd.Abstractions.Records;
using Klassd.Core.Localization;
using Klassd.Core.Models;
using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Backoffice.Modules.Pages.Services;

namespace Klassd.Backoffice.State;

/// <summary>
/// The slide-in editor's state machine (create / edit / translate), ported from usePanel.ts:
/// slug auto-gen + parent prefix + per-locale preview, allowed-children filtering,
/// text/block field split, block add/edit/remove, compare loading, and save/delete.
/// </summary>
public sealed class EditorPanelState(
    PageService pages, ContentTypeCatalog catalog, LocaleState locale,
    PageTreeState tree, ToastService toasts, LocaleRegistry locales)
{
    public event Action? Changed;
    private void Notify() => Changed?.Invoke();

    // ── Panel / mode ──────────────────────────────────────────────────
    public bool PanelOpen { get; private set; }
    public string? EditingId { get; private set; }
    public string? PendingContentId { get; private set; }
    public string? PendingParentId { get; private set; }
    public string? PendingParentSlug { get; private set; }
    public IReadOnlyList<string>? AllowedChildTypes { get; private set; }

    // ── Form ──────────────────────────────────────────────────────────
    public string FormName { get; set; } = "";
    public string FormSlug { get; set; } = "";
    public string FormPageType { get; set; } = "";
    public Dictionary<string, string> FormData { get; private set; } = new();
    private bool _slugAutoFill;

    // ── Blocks ────────────────────────────────────────────────────────
    public Dictionary<string, List<BlockData>> PendingBlockAreas { get; private set; } = new();
    public string? ActiveBlockArea { get; private set; }
    public int? EditingBlockIndex { get; private set; }
    public bool AddBlockOpen { get; private set; }
    public string NewBlockType { get; set; } = "";
    public Dictionary<string, string> NewBlockData { get; private set; } = new();
    public bool NewBlockTypeDisabled { get; private set; }
    // Optional publish schedule for the block being added/edited (treated as UTC).
    public DateTime? NewBlockStartUtc { get; set; }
    public DateTime? NewBlockEndUtc { get; set; }
    public int NewBlockPriority { get; set; }

    // ── Compare ───────────────────────────────────────────────────────
    public bool CompareMode { get; private set; }
    public PageRecord? CompareDoc { get; private set; }
    public string CompareLocale { get; set; } = "";

    // ── Schedule preview (admin) ──────────────────────────────────────
    /// <summary>When set, the editor shows block schedule status as of this instant (UTC) instead of now.</summary>
    public DateTime? PreviewAtUtc { get; set; }
    /// <summary>The instant block schedule badges are evaluated at: the preview date, or now.</summary>
    public DateTime PreviewInstant => PreviewAtUtc ?? DateTime.UtcNow;

    // ── Market time zone (schedule wall-clock is authored in the page's market) ─
    /// <summary>The time zone of the page being edited (its locale's market). Schedule times are wall-clock here.</summary>
    public TimeZoneInfo MarketTimeZone => locales.TimeZoneFor(locale.SelectedLocale);

    /// <summary>Label for the market zone at "now", e.g. "Europe/Berlin (UTC+01:00)".</summary>
    public string MarketTimeZoneLabel => MarketTime.Label(MarketTimeZone, DateTime.UtcNow);

    // Editor-facing wall-clock views of the UTC schedule fields (datetime-local inputs bind to these).
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

    public DateTime? PreviewAtLocal
    {
        get => PreviewAtUtc is { } u ? MarketTime.ToLocal(u, MarketTimeZone) : null;
        set => PreviewAtUtc = value is { } v ? MarketTime.ToUtc(v, MarketTimeZone) : null;
    }

    // ── Computed ──────────────────────────────────────────────────────
    public bool HasContent => EditingId is not null || PendingContentId is not null;
    public PageTypeInfo? CurrentPageType => catalog.GetPageType(FormPageType);
    public IReadOnlyList<PageFieldInfo> CurrentTextFields =>
        CurrentPageType?.Fields.Where(f => f.FieldType != "blocks").ToList() ?? [];
    public IReadOnlyList<PageFieldInfo> CurrentBlockFields =>
        CurrentPageType?.Fields.Where(f => f.FieldType == "blocks").ToList() ?? [];
    public BlockTypeInfo? NewBlockTypeDef => catalog.GetBlockType(NewBlockType);

    public IReadOnlyList<PageTypeInfo> FilteredPageTypes =>
        AllowedChildTypes is null
            ? catalog.PageTypes
            : catalog.PageTypes.Where(p => AllowedChildTypes.Contains(p.TypeName)).ToList();

    public string PanelTitle =>
        EditingId is not null ? "Edit Page" : PendingParentId is not null ? "New Child Page" : "New Page";

    public string? PendingParentName =>
        PendingParentId is null ? null : tree.Pages.FirstOrDefault(p => p.Id == PendingParentId)?.Name;

    public string ActiveBlockAreaLabel =>
        CurrentBlockFields.FirstOrDefault(f => f.Name == ActiveBlockArea)?.DisplayName ?? ActiveBlockArea ?? "";

    public string SlugPrefix =>
        PendingParentSlug is not null ? $"/{PendingParentSlug}/" : "/";

    public string FullSlug =>
        PendingParentSlug is null || PendingParentSlug.Length == 0
            ? FormSlug
            : $"{PendingParentSlug}/{FormSlug}";

    public string SlugPreview
    {
        get
        {
            var prefix = locale.IsPrimary ? "" : locale.SelectedLocale + "/";
            return FormSlug.Length == 0 ? "/" + prefix.TrimEnd('/') : "/" + prefix + FullSlug;
        }
    }

    // Compare-side field split
    public PageTypeInfo? ComparePageType => CompareDoc is null ? null : catalog.GetPageType(CompareDoc.PageTypeName);
    public IReadOnlyList<PageFieldInfo> CompareTextFields =>
        ComparePageType?.Fields.Where(f => f.FieldType != "blocks").ToList() ?? [];
    public IReadOnlyList<PageFieldInfo> CompareBlockFields =>
        ComparePageType?.Fields.Where(f => f.FieldType == "blocks").ToList() ?? [];

    // ── Open / close ──────────────────────────────────────────────────
    public void OpenForCreate(string? parentId = null, PageRecord? reference = null,
        IReadOnlyList<string>? allowedTypes = null)
    {
        ResetCommon();
        EditingId = null;
        PendingContentId = reference?.ContentId;
        PendingParentId = parentId;
        PendingParentSlug = parentId is null ? null : tree.Pages.FirstOrDefault(p => p.Id == parentId)?.Slug ?? "";
        AllowedChildTypes = allowedTypes;
        _slugAutoFill = true;

        if (reference is not null)
        {
            FormPageType = reference.PageTypeName;
            CompareDoc = reference;
            CompareMode = true;
            CompareLocale = reference.LocaleCode;
        }
        PanelOpen = true;
        Notify();
    }

    public void OpenForEdit(PageRecord page)
    {
        ResetCommon();
        EditingId = page.Id;
        _slugAutoFill = false;
        PendingParentSlug = page.ParentId is null ? null
            : tree.Pages.FirstOrDefault(p => p.Id == page.ParentId)?.Slug ?? "";

        FormName = page.Name;
        FormPageType = page.PageTypeName;
        FormData = new Dictionary<string, string>(page.Data);
        FormSlug = StripParentPrefix(page.Slug, PendingParentSlug);
        PendingBlockAreas = page.BlockAreas.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(b => new BlockData(
                b.BlockTypeName, new Dictionary<string, string>(b.Data), b.StartUtc, b.EndUtc, b.Priority)).ToList());
        PanelOpen = true;
        Notify();
    }

    public void OpenForTranslate(PageRecord primary) => OpenForCreate(primary.ParentId, primary);

    public void Close()
    {
        ResetCommon();
        EditingId = null;
        PendingContentId = PendingParentId = PendingParentSlug = null;
        AllowedChildTypes = null;
        CompareMode = false;
        CompareDoc = null;
        CompareLocale = "";
        PanelOpen = false;
        Notify();
    }

    private void ResetCommon()
    {
        FormName = FormSlug = FormPageType = "";
        FormData = new();
        PendingBlockAreas = new();
        AddBlockOpen = false;
        ActiveBlockArea = null;
        EditingBlockIndex = null;
        NewBlockType = "";
        NewBlockData = new();
        NewBlockTypeDisabled = false;
        NewBlockStartUtc = NewBlockEndUtc = null;
        NewBlockPriority = 0;
        PreviewAtUtc = null;
    }

    // ── Slug behavior ─────────────────────────────────────────────────
    public void OnNameChanged()
    {
        if (_slugAutoFill) FormSlug = GenerateSlug(FormName);
        Notify();
    }

    public void OnPageTypeChanged()
    {
        var pt = CurrentPageType;
        if (pt?.DefaultSlug is not null)
        {
            FormSlug = pt.DefaultSlug;
            _slugAutoFill = false;
        }
        Notify();
    }

    public void OnSlugInput() => _slugAutoFill = false;

    private static string GenerateSlug(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '-') sb.Append('-');
        }
        var slug = sb.ToString();
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }

    private static string StripParentPrefix(string slug, string? parentSlug) =>
        !string.IsNullOrEmpty(parentSlug) && slug.StartsWith(parentSlug + "/")
            ? slug[(parentSlug.Length + 1)..] : slug;

    // ── Compare loading ───────────────────────────────────────────────
    public async Task OnCompareLocaleChangeAsync(string code)
    {
        CompareLocale = code;
        if (string.IsNullOrEmpty(code)) { CompareMode = false; CompareDoc = null; Notify(); return; }

        var contentId = EditingId is not null
            ? tree.Pages.FirstOrDefault(p => p.Id == EditingId)?.ContentId
            : PendingContentId;

        if (contentId is not null)
        {
            var siblings = await pages.GetByContentIdAsync(contentId);
            CompareDoc = siblings.FirstOrDefault(p => p.LocaleCode == code);
            CompareMode = true;
        }
        Notify();
    }

    // ── Block editing ─────────────────────────────────────────────────
    public void ShowAddBlock(string areaName)
    {
        ActiveBlockArea = areaName;
        EditingBlockIndex = null;
        NewBlockType = "";
        NewBlockData = new();
        NewBlockTypeDisabled = false;
        NewBlockStartUtc = NewBlockEndUtc = null;
        NewBlockPriority = 0;
        AddBlockOpen = true;
        Notify();
    }

    public void HideAddBlock()
    {
        AddBlockOpen = false;
        ActiveBlockArea = null;
        EditingBlockIndex = null;
        NewBlockType = "";
        NewBlockData = new();
        NewBlockTypeDisabled = false;
        NewBlockStartUtc = NewBlockEndUtc = null;
        NewBlockPriority = 0;
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
        var block = PendingBlockAreas[areaName][index];
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
        var list = PendingBlockAreas.TryGetValue(ActiveBlockArea, out var existing) ? existing : PendingBlockAreas[ActiveBlockArea] = [];
        if (EditingBlockIndex is { } i) list[i] = entry;
        else list.Add(entry);
        HideAddBlock();
    }

    public void RemoveBlock(string areaName, int index)
    {
        if (PendingBlockAreas.TryGetValue(areaName, out var list) && index < list.Count)
            list.RemoveAt(index);
        Notify();
    }

    public IReadOnlyList<BlockData> BlocksIn(string areaName) =>
        PendingBlockAreas.TryGetValue(areaName, out var list) ? list : [];

    // ── Save / delete ─────────────────────────────────────────────────
    public async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FormPageType) || string.IsNullOrWhiteSpace(FormName))
        {
            toasts.Error("Page type and name are required.");
            return false;
        }
        try
        {
            if (EditingId is not null)
                await pages.UpdateAsync(EditingId, new UpdatePageRequest(FormName, FullSlug, FormData, PendingBlockAreas));
            else
                await pages.CreateAsync(new CreatePageRequest(
                    FormPageType, locale.SelectedLocale,
                    string.IsNullOrEmpty(PendingContentId) ? null : PendingContentId,
                    PendingParentId, FormName, FullSlug, FormData, PendingBlockAreas));

            toasts.Success(EditingId is not null ? "Page updated" : "Page created");
            await tree.LoadAsync();
            Close();
            return true;
        }
        catch (InvalidOperationException ex) { toasts.Error(ex.Message); return false; }
        catch (Exception) { toasts.Error("Save failed."); return false; }
    }

    public async Task DeleteAsync(string id)
    {
        if (await pages.DeleteAsync(id)) { toasts.Success("Page deleted"); await tree.LoadAsync(); }
        else toasts.Error("Delete failed.");
    }
}
