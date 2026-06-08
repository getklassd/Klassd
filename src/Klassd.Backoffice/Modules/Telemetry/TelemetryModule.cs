using Klassd.Backoffice.Modules.Telemetry.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Klassd.Backoffice.Modules.Telemetry;

/// <summary>
/// Anonymous usage telemetry. Registers the state store, snapshot service, default HTTP sink, and the
/// one-time startup reporter. No API endpoints — the admin Settings page talks to the service directly.
/// </summary>
public sealed class TelemetryModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TelemetryStateStore>();
        services.AddScoped<TelemetryService>();
        services.TryAddSingleton<ITelemetrySink, HttpTelemetrySink>();
        services.AddHttpClient(HttpTelemetrySink.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddHostedService<TelemetryStartupReporter>();
    }

    public void MapEndpoints(IEndpointRouteBuilder routes) { }
}
