using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Core.Models;

namespace Klassd.Backoffice.State;

/// <summary>
/// The block-editing surface that <c>BlockAreaSection</c> / <c>BlockEditor</c> bind to (via a cascading
/// value), so the same components edit blocks for pages (<see cref="EditorPanelState"/>) and for globals
/// (<see cref="GlobalEditorState"/>) without forking.
/// </summary>
public interface IBlockAreaHost
{
    event Action? Changed;

    IReadOnlyList<BlockData> BlocksIn(string areaName);

    string? ActiveBlockArea { get; }
    int? EditingBlockIndex { get; }
    bool AddBlockOpen { get; }
    string ActiveBlockAreaLabel { get; }
    void ShowAddBlock(string areaName);
    void HideAddBlock();
    void EditBlock(string areaName, int index);
    void RemoveBlock(string areaName, int index);
    void CommitBlock();

    string NewBlockType { get; set; }
    bool NewBlockTypeDisabled { get; }
    Dictionary<string, string> NewBlockData { get; }
    BlockTypeInfo? NewBlockTypeDef { get; }
    void OnNewBlockTypeChanged();
    int NewBlockPriority { get; set; }
    DateTime? NewBlockStartLocal { get; set; }
    DateTime? NewBlockEndLocal { get; set; }
    DateTime? NewBlockStartUtc { get; }
    DateTime? NewBlockEndUtc { get; }

    TimeZoneInfo MarketTimeZone { get; }
    string MarketTimeZoneLabel { get; }
    DateTime PreviewInstant { get; }
    DateTime? PreviewAtUtc { get; }
}
