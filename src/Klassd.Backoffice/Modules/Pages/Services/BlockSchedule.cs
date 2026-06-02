using Klassd.Abstractions.Records;

namespace Klassd.Backoffice.Modules.Pages.Services;

/// <summary>
/// Resolves which scheduled blocks are live. A block is active when <paramref name="nowUtc"/> falls
/// within its (optional) <see cref="BlockInstanceRecord.StartUtc"/>/<see cref="BlockInstanceRecord.EndUtc"/>
/// window; open-ended bounds mean "from the beginning" / "forever" (an always-on fallback). Used by the
/// headless delivery projection — the admin always works with the full, unfiltered block set.
/// </summary>
public static class BlockSchedule
{
    public static bool IsActive(BlockInstanceRecord block, DateTime nowUtc) =>
        (block.StartUtc is null || block.StartUtc <= nowUtc) &&
        (block.EndUtc is null || nowUtc < block.EndUtc);

    /// <summary>
    /// The blocks active at <paramref name="nowUtc"/>, highest <see cref="BlockInstanceRecord.Priority"/>
    /// first. LINQ's stable ordering keeps authored order within a priority (so the default priority 0
    /// just preserves authoring order).
    /// </summary>
    public static List<BlockInstanceRecord> Active(IEnumerable<BlockInstanceRecord> blocks, DateTime nowUtc) =>
        blocks.Where(b => IsActive(b, nowUtc)).OrderByDescending(b => b.Priority).ToList();
}
