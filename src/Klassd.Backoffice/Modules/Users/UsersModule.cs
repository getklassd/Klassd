using Klassd.Backoffice.Modules.Auth.Services;
using Klassd.Backoffice.Modules.Preferences.Services;

namespace Klassd.Backoffice.Modules.Users;

public class UsersModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration) { }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/api/users").RequireAuthorization();

        api.MapGet("/", async (UserService userService) =>
        {
            var users = await userService.GetAllAsync();
            return Results.Ok(users.Select(u => new { u.Id, u.Username }));
        });

        api.MapGet("/{id}/preferences", async (string id, PreferencesService prefsService) =>
        {
            var prefs = await prefsService.GetAsync(id);
            return prefs is null ? Results.NotFound() : Results.Ok(prefs);
        });
    }
}
