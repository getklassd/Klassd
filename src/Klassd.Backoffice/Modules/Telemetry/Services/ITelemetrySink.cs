using System.Net.Http.Json;
using Klassd.Backoffice.Modules.Telemetry.Models;
using Microsoft.Extensions.Logging;

namespace Klassd.Backoffice.Modules.Telemetry.Services;

/// <summary>
/// Where a telemetry snapshot is delivered. Swap this out (AddSingleton&lt;ITelemetrySink, …&gt;) to
/// route telemetry to a custom destination instead of the default HTTP POST.
/// </summary>
public interface ITelemetrySink
{
    Task SendAsync(TelemetrySnapshot snapshot, CancellationToken ct = default);
}

/// <summary>
/// Default sink: POSTs the snapshot as JSON to the configured endpoint. When no endpoint is
/// configured it no-ops (logs at Debug) so telemetry is safe to ship before the collector exists.
/// </summary>
public sealed class HttpTelemetrySink(
    IHttpClientFactory httpFactory,
    CmsOptions options,
    ILogger<HttpTelemetrySink> logger) : ITelemetrySink
{
    public const string HttpClientName = "klassd-telemetry";

    public async Task SendAsync(TelemetrySnapshot snapshot, CancellationToken ct = default)
    {
        var endpoint = options.TelemetryEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            logger.LogDebug(
                "Klassd telemetry: no endpoint configured (Klassd:Telemetry:Endpoint) — snapshot built but not sent. InstallId={InstallId}",
                snapshot.InstallId);
            return;
        }

        var client = httpFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsJsonAsync(endpoint, snapshot, ct);
        logger.LogDebug("Klassd telemetry: sent snapshot to {Endpoint} ({Status}).", endpoint, (int)response.StatusCode);
    }
}
