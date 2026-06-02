using Klassd.Backoffice.Modules.Pages.Services;

namespace Klassd.UnitTests;

public class SchedulePreviewTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Returns_now_when_preview_disabled_even_if_param_present()
    {
        var result = SchedulePreview.Resolve(enabled: false, "2026-12-24T00:00:00Z", Now);
        await Assert.That(result).IsEqualTo(Now); // production: ?preview is ignored
    }

    [Test]
    public async Task Returns_now_when_no_param()
    {
        await Assert.That(SchedulePreview.Resolve(enabled: true, null, Now)).IsEqualTo(Now);
        await Assert.That(SchedulePreview.Resolve(enabled: true, "", Now)).IsEqualTo(Now);
    }

    [Test]
    public async Task Parses_iso_utc_when_enabled()
    {
        var result = SchedulePreview.Resolve(enabled: true, "2026-12-24T10:00:00Z", Now);
        await Assert.That(result).IsEqualTo(new DateTime(2026, 12, 24, 10, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task Bare_datetime_is_treated_as_utc()
    {
        var result = SchedulePreview.Resolve(enabled: true, "2026-12-24T10:00:00", Now);
        await Assert.That(result).IsEqualTo(new DateTime(2026, 12, 24, 10, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task Offset_is_normalized_to_utc()
    {
        // 10:00 +02:00 => 08:00Z
        var result = SchedulePreview.Resolve(enabled: true, "2026-12-24T10:00:00+02:00", Now);
        await Assert.That(result).IsEqualTo(new DateTime(2026, 12, 24, 8, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task Falls_back_to_now_on_garbage()
    {
        await Assert.That(SchedulePreview.Resolve(enabled: true, "not-a-date", Now)).IsEqualTo(Now);
    }
}
