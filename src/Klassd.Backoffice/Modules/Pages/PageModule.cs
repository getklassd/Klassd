using Klassd.Backoffice.Modules.Auth;
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
        services.AddScoped<ReferenceResolver>();
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
            return Results.Ok(PageDelivery.ProjectLive(await svc.GetByLocaleAsync(code), DeliveryInstant(ctx, opts)));
        }).AsPublicDelivery();

        // Literal segments must be registered before the wildcard {id} route.
        api.MapGet("/pages/content/{contentId}", async (string contentId, PageService svc, HttpContext ctx, CmsOptions opts) =>
            Results.Ok(PageDelivery.ProjectLive(await svc.GetByContentIdAsync(contentId), DeliveryInstant(ctx, opts))))
            .AsPublicDelivery();

        // Fetch one page by its (locale-unique) slug. {**slug} is a catch-all so nested slugs work
        // (e.g. /api/pages/by-slug/products/shoes). ?depth=1 resolves page/media references to URLs.
        api.MapGet("/pages/by-slug/{**slug}", async (string slug, string? locale, int? depth, string? expand,
            PageService svc, ReferenceResolver resolver, LocaleRegistry registry, HttpContext ctx, CmsOptions opts) =>
        {
            var code = locale ?? registry.All.FirstOrDefault(l => l.Mandatory)?.Code ?? "en";
            var now = DeliveryInstant(ctx, opts);
            var page = await svc.GetBySlugAsync(code, slug);
            if (page is null || !PageSchedule.IsLive(page, now)) return Results.NotFound();
            var projected = PageDelivery.Project(page, now);
            return Results.Ok(await resolver.ResolveAsync(projected, depth ?? 0, ParseExpand(expand), ctx.RequestAborted));
        }).AsPublicDelivery();

        api.MapGet("/pages/{id}", async (string id, int? depth, string? expand,
            PageService svc, ReferenceResolver resolver, HttpContext ctx, CmsOptions opts) =>
        {
            var now = DeliveryInstant(ctx, opts);
            var page = await svc.GetByIdAsync(id);
            // Outside its publish window the page does not exist for headless consumers.
            if (page is null || !PageSchedule.IsLive(page, now)) return Results.NotFound();
            var projected = PageDelivery.Project(page, now);
            return Results.Ok(await resolver.ResolveAsync(projected, depth ?? 0, ParseExpand(expand), ctx.RequestAborted));
        }).AsPublicDelivery();

        api.MapGet("/pages/{id}/translations", async (string id, PageService svc, HttpContext ctx, CmsOptions opts) =>
        {
            var page = await svc.GetByIdAsync(id);
            if (page is null) return Results.NotFound();
            return Results.Ok(PageDelivery.ProjectLive(await svc.GetByContentIdAsync(page.ContentId), DeliveryInstant(ctx, opts)));
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
        }).RequireCapability(Capabilities.PagesEdit);

        // Saves the page's DRAFT — does not publish. The published snapshot delivery serves is
        // unchanged until POST /pages/{id}/publish. (Draft-first model; see PageService.)
        api.MapPut("/pages/{id}", async (string id, UpdatePageRequest req, PageService svc, HttpContext ctx) =>
        {
            try
            {
                var page = await svc.SaveDraftAsync(id, req, ctx.User.Identity?.Name);
                return page is null ? Results.NotFound() : Results.Ok(page);
            }
            catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
        }).RequireCapability(Capabilities.PagesEdit);

        api.MapGet("/pages/{id}/versions", async (string id, PageService svc) =>
            Results.Ok(await svc.GetHistoryAsync(id))).RequireCapability(Capabilities.PagesEdit);

        api.MapPost("/pages/{id}/versions/{versionId}/restore", async (string id, string versionId, PageService svc, HttpContext ctx) =>
        {
            var page = await svc.RestoreVersionAsync(id, versionId, ctx.User.Identity?.Name);
            return page is null ? Results.NotFound() : Results.Ok(page);
        }).RequireCapability(Capabilities.PagesEdit);

        api.MapPost("/pages/{id}/publish", async (string id, PageService svc, HttpContext ctx) =>
        {
            try
            {
                var page = await svc.PublishAsync(id, ctx.User.Identity?.Name);
                return page is null ? Results.NotFound() : Results.Ok(page);
            }
            catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
        }).RequireCapability(Capabilities.PagesPublish);

        api.MapPost("/pages/{id}/unpublish", async (string id, PageService svc) =>
        {
            var page = await svc.UnpublishAsync(id);
            return page is null ? Results.NotFound() : Results.Ok(page);
        }).RequireCapability(Capabilities.PagesPublish);

        api.MapDelete("/pages/{id}/draft", async (string id, PageService svc) =>
        {
            await svc.DiscardDraftAsync(id);
            return Results.NoContent();
        }).RequireCapability(Capabilities.PagesEdit);

        api.MapDelete("/pages/{id}", async (string id, PageService svc) =>
        {
            var deleted = await svc.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireCapability(Capabilities.PagesEdit);
    }

    /// <summary>The instant to resolve scheduling at: ?preview=&lt;datetime&gt; when allowed, else now.</summary>
    private static DateTime DeliveryInstant(HttpContext ctx, CmsOptions opts) =>
        SchedulePreview.Resolve(opts.AllowSchedulePreview, ctx.Request.Query["preview"], DateTime.UtcNow);

    /// <summary>Parses <c>?expand=a,b,c</c> into a field-name set; null/empty ⇒ null (resolve all reference fields).</summary>
    private static IReadOnlySet<string>? ParseExpand(string? expand) =>
        string.IsNullOrWhiteSpace(expand)
            ? null
            : expand.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
}
