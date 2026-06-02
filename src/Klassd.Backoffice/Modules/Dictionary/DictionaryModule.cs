using Klassd.Backoffice.Modules.Dictionary.Services;

namespace Klassd.Backoffice.Modules.Dictionary;

/// <summary>
/// Translation dictionary endpoints. The admin manages entries in-process via <see cref="DictionaryService"/>;
/// the headless frontend fetches a resolved per-locale map.
/// </summary>
public sealed class DictionaryModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<DictionaryService>();

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/api/dictionary").RequireAuthorization();

        // Frontend delivery: resolved key→value map for a locale (values follow the fallback chain).
        // Anonymous + CORS, like page delivery; management below stays cookie-protected.
        api.MapGet("/resolved/{locale}", async (string locale, DictionaryService svc) =>
            Results.Ok(await svc.ResolveAsync(locale)))
            .AsPublicDelivery();

        // Management (also surfaced in the admin UI in-process).
        api.MapGet("/", async (DictionaryService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        api.MapPut("/{key}", async (string key, Dictionary<string, string> values, DictionaryService svc) =>
        {
            try { await svc.UpsertAsync(key, values); return Results.NoContent(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
        });

        api.MapDelete("/{key}", async (string key, DictionaryService svc) =>
            await svc.DeleteAsync(key) ? Results.NoContent() : Results.NotFound());
    }
}
