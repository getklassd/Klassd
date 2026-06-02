using Klassd.Abstractions.Records;
using Klassd.Backoffice.Modules.Globals.Services;
using Klassd.Backoffice.Modules.Pages.Services;   // BlockSchedule
using Klassd.Core.Localization;

namespace Klassd.Backoffice.Modules.Globals;

/// <summary>A global type's name + display name (delivery list).</summary>
public sealed record GlobalSummary(string TypeName, string DisplayName);

/// <summary>Resolved global content for delivery: data + schedule-filtered block areas.</summary>
public sealed record GlobalDeliveryResponse(
    string TypeName, string Locale, Dictionary<string, string> Data,
    Dictionary<string, List<BlockInstanceRecord>> BlockAreas, DateTime UpdatedAt);

/// <summary>Headless delivery for globals. The admin edits in-process via <see cref="GlobalService"/>,
/// so only anonymous delivery endpoints are mapped here.</summary>
public class GlobalModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<GlobalService>();

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        // List of global type names (public; handy for tooling/discovery).
        routes.MapGet("/api/globals", (GlobalService svc) =>
            Results.Ok(svc.ListTypes().Select(t => new GlobalSummary(t.TypeName, t.DisplayName)).ToList()))
            .AsPublicDelivery();

        // Resolved global content by type name + locale (locale fallback applied), block areas
        // filtered to the blocks live now — same delivery contract as pages.
        routes.MapGet("/api/globals/{name}", async (string name, string? locale,
            GlobalService svc, LocaleRegistry registry) =>
        {
            var code = locale ?? registry.All.FirstOrDefault()?.Code ?? "en";
            var rec = await svc.GetForDeliveryAsync(name, code);
            if (rec is null) return Results.NotFound();

            var nowUtc = DateTime.UtcNow;
            return Results.Ok(new GlobalDeliveryResponse(
                rec.TypeName, rec.LocaleCode, rec.Data,
                rec.BlockAreas.ToDictionary(a => a.Key, a => BlockSchedule.Active(a.Value, nowUtc)),
                rec.UpdatedAt));
        }).AsPublicDelivery();
    }
}
