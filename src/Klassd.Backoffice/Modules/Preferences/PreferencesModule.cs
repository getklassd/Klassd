using System.Security.Claims;
using Klassd.Abstractions.Records;
using Klassd.Backoffice.Modules.Preferences.Models;
using Klassd.Backoffice.Modules.Preferences.Services;

namespace Klassd.Backoffice.Modules.Preferences;

public class PreferencesModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<PreferencesService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/api").RequireAuthorization();

        api.MapGet("/preferences", async (HttpContext ctx, PreferencesService svc) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var prefs = await svc.GetAsync(userId) ?? new UserPreferencesRecord { UserId = userId };
            return Results.Ok(prefs);
        });

        api.MapPut("/preferences", async (HttpContext ctx, UpdatePreferencesRequest req, PreferencesService svc) =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return Results.Ok(await svc.UpsertAsync(userId, req));
        });
    }
}
