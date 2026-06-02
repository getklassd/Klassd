using Klassd.Backoffice.Modules.Pages.Models;
using Klassd.Backoffice.Modules.Pages.Services;
using Klassd.Core.Localization;
using Klassd.Core.Services;

namespace Klassd.Backoffice.Modules.Pages;

public class PageModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<PageService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/api").RequireAuthorization();

        api.MapGet("/locales", (LocaleRegistry registry) =>
            Results.Ok(registry.All));

        api.MapGet("/page-types", (PageTypeRegistry registry) =>
            Results.Ok(registry.GetAll()));

        api.MapGet("/block-types", (BlockTypeRegistry registry) =>
            Results.Ok(registry.GetAll()));

        // Headless content delivery — anonymous + CORS (writes/admin below stay cookie-protected).
        // GETs deliver only the blocks live right now (see PageDelivery/BlockSchedule); scheduling
        // resolves per request, so a cached page still serves correct content. When
        // Klassd:AllowSchedulePreview is on, ?preview=<UTC datetime> time-travels the schedule.
        api.MapGet("/pages", async (string? locale, PageService svc, LocaleRegistry registry, HttpContext ctx, CmsOptions opts) =>
        {
            var code = locale ?? registry.All.FirstOrDefault(l => l.Mandatory)?.Code ?? "en";
            return Results.Ok(PageDelivery.Project(await svc.GetByLocaleAsync(code), DeliveryInstant(ctx, opts)));
        }).AsPublicDelivery();

        // Literal segment "content" must be registered before the wildcard {id} route
        api.MapGet("/pages/content/{contentId}", async (string contentId, PageService svc, HttpContext ctx, CmsOptions opts) =>
            Results.Ok(PageDelivery.Project(await svc.GetByContentIdAsync(contentId), DeliveryInstant(ctx, opts))))
            .AsPublicDelivery();

        api.MapGet("/pages/{id}", async (string id, PageService svc, HttpContext ctx, CmsOptions opts) =>
        {
            var page = await svc.GetByIdAsync(id);
            return page is null ? Results.NotFound() : Results.Ok(PageDelivery.Project(page, DeliveryInstant(ctx, opts)));
        }).AsPublicDelivery();

        api.MapGet("/pages/{id}/translations", async (string id, PageService svc, HttpContext ctx, CmsOptions opts) =>
        {
            var page = await svc.GetByIdAsync(id);
            if (page is null) return Results.NotFound();
            return Results.Ok(PageDelivery.Project(await svc.GetByContentIdAsync(page.ContentId), DeliveryInstant(ctx, opts)));
        }).AsPublicDelivery();

        api.MapPost("/pages", async (CreatePageRequest req, PageService svc, PageTypeRegistry registry) =>
        {
            if (!registry.Exists(req.PageTypeName))
                return Results.BadRequest($"Unknown page type: {req.PageTypeName}");
            try
            {
                var page = await svc.CreateAsync(req);
                return Results.Created($"/api/pages/{page.Id}", page);
            }
            catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
        });

        api.MapPut("/pages/{id}", async (string id, UpdatePageRequest req, PageService svc) =>
        {
            try
            {
                var page = await svc.UpdateAsync(id, req);
                return page is null ? Results.NotFound() : Results.Ok(page);
            }
            catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
        });

        api.MapDelete("/pages/{id}", async (string id, PageService svc) =>
        {
            var deleted = await svc.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }

    /// <summary>The instant to resolve scheduling at: ?preview=&lt;datetime&gt; when allowed, else now.</summary>
    private static DateTime DeliveryInstant(HttpContext ctx, CmsOptions opts) =>
        SchedulePreview.Resolve(opts.AllowSchedulePreview, ctx.Request.Query["preview"], DateTime.UtcNow);
}
