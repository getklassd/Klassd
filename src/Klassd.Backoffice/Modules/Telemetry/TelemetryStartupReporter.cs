using Klassd.Backoffice.Modules.Telemetry.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Klassd.Backoffice.Modules.Telemetry;

/// <summary>
/// Sends one anonymous usage snapshot shortly after startup (and prints the one-time opt-out notice
/// on first run). Fully best-effort and out of the request path — any failure is swallowed so it can
/// never break or slow the host.
/// </summary>
public sealed class TelemetryStartupReporter(
    IServiceScopeFactory scopes,
    ILogger<TelemetryStartupReporter> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            // Let the host finish coming up before doing anything; keeps startup snappy.
            await Task.Delay(TimeSpan.FromSeconds(5), ct);

            using var scope = scopes.CreateScope();
            var sp = scope.ServiceProvider;
            var telemetry = sp.GetRequiredService<TelemetryService>();
            var state = sp.GetRequiredService<TelemetryStateStore>();

            var (enabled, source) = await telemetry.ResolveAsync(ct);
            if (!enabled)
            {
                logger.LogDebug("Klassd telemetry: disabled ({Source}) — nothing sent.", source);
                return;
            }

            await ShowFirstRunNoticeOnceAsync(state, ct);

            var snapshot = await telemetry.BuildSnapshotAsync(ct);
            await sp.GetRequiredService<ITelemetrySink>().SendAsync(snapshot, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Klassd telemetry: startup report failed (ignored).");
        }
    }

    private async Task ShowFirstRunNoticeOnceAsync(TelemetryStateStore store, CancellationToken ct)
    {
        if ((await store.LoadAsync(ct)).NoticeShownAtUtc is not null) return;

        logger.LogInformation(
            "Klassd collects anonymous usage telemetry (version, which storage/cache/media adapters and " +
            "features are enabled, and aggregate type counts) to guide development. It contains no content, " +
            "credentials, or hostnames. Opt out any time: set Klassd:Telemetry:Enabled=false, toggle it off in " +
            "Admin → Settings, or set the environment variable {EnvVar}=1.",
            TelemetryService.OptOutEnvVar);

        await store.UpdateAsync(s => s.NoticeShownAtUtc = DateTimeOffset.UtcNow, ct);
    }
}
