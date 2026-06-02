using Klassd.Abstractions.Records;
using Klassd.Backoffice.Modules.Pages.Services;

namespace Klassd.UnitTests;

public class BlockScheduleTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static BlockInstanceRecord Block(string name, DateTime? start = null, DateTime? end = null, int priority = 0) =>
        new() { BlockTypeName = name, StartUtc = start, EndUtc = end, Priority = priority };

    [Test]
    public async Task No_bounds_is_always_active()
    {
        await Assert.That(BlockSchedule.IsActive(Block("a"), Now)).IsTrue();
    }

    [Test]
    public async Task Active_only_within_the_window()
    {
        var b = Block("a", start: Now.AddDays(-1), end: Now.AddDays(1));
        await Assert.That(BlockSchedule.IsActive(b, Now)).IsTrue();
        await Assert.That(BlockSchedule.IsActive(b, Now.AddDays(-2))).IsFalse(); // before start
        await Assert.That(BlockSchedule.IsActive(b, Now.AddDays(2))).IsFalse();  // after end
    }

    [Test]
    public async Task End_is_exclusive_start_is_inclusive()
    {
        var b = Block("a", start: Now, end: Now.AddHours(1));
        await Assert.That(BlockSchedule.IsActive(b, Now)).IsTrue();              // == start → active
        await Assert.That(BlockSchedule.IsActive(b, Now.AddHours(1))).IsFalse(); // == end → expired
    }

    [Test]
    public async Task No_end_date_is_an_open_ended_fallback()
    {
        var b = Block("fallback", start: Now.AddYears(-1)); // started long ago, never expires
        await Assert.That(BlockSchedule.IsActive(b, Now)).IsTrue();
    }

    [Test]
    public async Task Active_filters_out_inactive_blocks()
    {
        var blocks = new[]
        {
            Block("live"),                                              // always on
            Block("future", start: Now.AddDays(1)),                    // not yet
            Block("past", end: Now.AddDays(-1)),                       // expired
            Block("promo", start: Now.AddDays(-1), end: Now.AddDays(1)),// in window
        };

        var active = BlockSchedule.Active(blocks, Now);
        await Assert.That(active.Select(b => b.BlockTypeName)).IsEquivalentTo(new[] { "live", "promo" });
    }

    [Test]
    public async Task Higher_priority_comes_first_ties_keep_authored_order()
    {
        var blocks = new[]
        {
            Block("a", priority: 0),
            Block("b", priority: 10),
            Block("c", priority: 0),
            Block("d", priority: 5),
        };

        var ordered = BlockSchedule.Active(blocks, Now).Select(b => b.BlockTypeName).ToList();
        await Assert.That(ordered).IsEquivalentTo(new[] { "b", "d", "a", "c" }); // 10, 5, then authored (a before c)
    }

    [Test]
    public async Task Project_filters_block_areas_without_mutating_the_source()
    {
        var page = new PageRecord
        {
            Id = "p1",
            BlockAreas = new()
            {
                ["hero"] = [Block("live"), Block("expired", end: Now.AddDays(-1))],
            },
        };

        var delivered = PageDelivery.Project(page, Now);

        await Assert.That(delivered.BlockAreas["hero"].Select(b => b.BlockTypeName)).IsEquivalentTo(new[] { "live" });
        // Source page is untouched (cache safety).
        await Assert.That(page.BlockAreas["hero"].Count).IsEqualTo(2);
        await Assert.That(ReferenceEquals(delivered, page)).IsFalse();
    }
}
