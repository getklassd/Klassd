using Klassd.Backoffice.Modules.Globals.Services;
using Klassd.Backoffice.Modules.Pages.Services;   // BlockSchedule
using Klassd.Core.Localization;

namespace Klassd.Backoffice.Modules.Globals;

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
            Results.Ok(svc.ListTypes().Select(t => new { t.TypeName, t.DisplayName }))).AsPublicDelivery();

        // Resolved global content by type name + locale (locale fallback applied), block areas
        // filtered to the blocks live now — same delivery contract as pages.
        routes.MapGet("/api/globals/{name}", async (string name, string? locale,
            GlobalService svc, LocaleRegistry registry) =>
        {
            var code = locale ?? registry.All.FirstOrDefault()?.Code ?? "en";
            var rec = await svc.GetForDeliveryAsync(name, code);
            if (rec is null) return Results.NotFound();

            var nowUtc = DateTime.UtcNow;
            return Results.Ok(new
            {
                typeName = rec.TypeName,
                locale = rec.LocaleCode,
                data = rec.Data,
                blockAreas = rec.BlockAreas.ToDictionary(a => a.Key, a => BlockSchedule.Active(a.Value, nowUtc)),
                updatedAt = rec.UpdatedAt,
            });
        }).AsPublicDelivery();
    }
}
