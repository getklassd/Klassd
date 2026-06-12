using Klassd.Auth.Core.Modules.Users;
using Klassd.Backoffice.Modules.Auth;
using Klassd.Backoffice.Modules.Preferences.Services;

namespace Klassd.Backoffice.Modules.Users;

public class UsersModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration) { }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/api/users").RequireAuthorization();

        api.MapGet("/", async (UserAccountService accounts, RolesService roles) =>
        {
            var users = await accounts.GetAllAsync();
            var result = new List<object>(users.Count);
            foreach (var u in users)
                result.Add(new { u.Id, Username = u.Username ?? u.PrimaryEmail, Roles = await roles.GetRolesAsync(u.Id) });
            return Results.Ok(result);
        }).RequireCapability(Capabilities.UsersManage);

        api.MapGet("/{id}/preferences", async (string id, PreferencesService prefsService) =>
        {
            var prefs = await prefsService.GetAsync(id);
            return prefs is null ? Results.NotFound() : Results.Ok(prefs);
        });
    }
}
