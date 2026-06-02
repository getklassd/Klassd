using Klassd.Backoffice.Modules.Pages.Services;
using Klassd.Core.Localization;

namespace Klassd.UnitTests;

public class MarketTimeTests
{
    // Fixed +04:00 zone (like Asia/Dubai, no DST) — deterministic without depending on the tz database.
    private static readonly TimeZoneInfo GulfPlus4 =
        TimeZoneInfo.CreateCustomTimeZone("Gulf+4", TimeSpan.FromHours(4), "Gulf+4", "Gulf+4");

    [Test]
    public async Task Market_midnight_maps_to_the_correct_utc_instant()
    {
        // 00:00 local in a +04:00 market is 20:00 UTC the previous day.
        var utc = MarketTime.ToUtc(new DateTime(2026, 6, 1, 0, 0, 0), GulfPlus4);
        await Assert.That(utc).IsEqualTo(new DateTime(2026, 5, 31, 20, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task ToLocal_is_the_inverse_of_ToUtc()
    {
        var local = new DateTime(2026, 12, 24, 9, 30, 0);
        var roundTrip = MarketTime.ToLocal(MarketTime.ToUtc(local, GulfPlus4), GulfPlus4);
        await Assert.That(roundTrip).IsEqualTo(local);
    }

    [Test]
    public async Task Utc_zone_is_identity()
    {
        var local = new DateTime(2026, 1, 2, 3, 4, 0);
        await Assert.That(MarketTime.ToUtc(local, TimeZoneInfo.Utc))
            .IsEqualTo(DateTime.SpecifyKind(local, DateTimeKind.Utc));
    }

    [Test]
    public async Task Two_markets_differ_for_the_same_wall_clock()
    {
        // "00:00" means a different absolute instant per market — the whole point of market time.
        var minus5 = TimeZoneInfo.CreateCustomTimeZone("M-5", TimeSpan.FromHours(-5), "M-5", "M-5");
        var midnight = new DateTime(2026, 6, 1, 0, 0, 0);

        var gulf = MarketTime.ToUtc(midnight, GulfPlus4);   // 2026-05-31 20:00Z
        var west = MarketTime.ToUtc(midnight, minus5);      // 2026-06-01 05:00Z
        await Assert.That(gulf).IsNotEqualTo(west);
        await Assert.That((west - gulf)).IsEqualTo(TimeSpan.FromHours(9));
    }

    [Test]
    public async Task Label_includes_zone_id_and_offset()
    {
        var label = MarketTime.Label(GulfPlus4, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        await Assert.That(label).IsEqualTo("Gulf+4 (UTC+04:00)");
    }

    [Test]
    public async Task LocaleRegistry_returns_utc_when_timezone_unset_or_unknown()
    {
        var registry = new LocaleRegistry(
        [
            new LocaleDefinition("en", Mandatory: true),                       // no timezone
            new LocaleDefinition("bogus", TimeZone: "Not/AZone"),              // unrecognized
        ]);

        await Assert.That(registry.TimeZoneFor("en")).IsEqualTo(TimeZoneInfo.Utc);
        await Assert.That(registry.TimeZoneFor("bogus")).IsEqualTo(TimeZoneInfo.Utc);
        await Assert.That(registry.TimeZoneFor("missing")).IsEqualTo(TimeZoneInfo.Utc);
    }

    [Test]
    public async Task UnresolvedTimeZones_flags_only_bad_configured_zones()
    {
        var registry = new LocaleRegistry(
        [
            new LocaleDefinition("en", Mandatory: true),            // no timezone → not flagged
            new LocaleDefinition("de", TimeZone: "Europe/Berlin"),  // resolvable → not flagged
            new LocaleDefinition("bad", TimeZone: "Not/AZone"),     // unresolvable → flagged
        ]);

        var unresolved = registry.UnresolvedTimeZones();
        await Assert.That(unresolved.Select(u => u.Code)).IsEquivalentTo(new[] { "bad" });
        await Assert.That(unresolved.Single().TimeZone).IsEqualTo("Not/AZone");
    }
}
