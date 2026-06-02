using System.Security.Claims;
using Klassd.Abstractions.Records;
using Klassd.Backoffice.Modules.Auth.Services;
using Microsoft.AspNetCore.Authentication;

namespace Klassd.Backoffice.Modules.Auth;

/// <summary>
/// Cookie-based authentication for the admin. Login/logout are form-post endpoints
/// (cookie <c>SignInAsync</c> needs an HttpContext, so they can't run in an interactive
/// circuit). External (SSO) logins challenge a registered scheme and provision/link a CMS
/// user in the callback.
/// </summary>
public class AuthModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<UserService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        // ── Local username/password ───────────────────────────────────
        // Form post from the static-SSR Login page (distinct path from the GET /admin/login page).
        routes.MapPost("/admin/auth/login", async (HttpContext ctx, UserService users, CmsOptions opts) =>
        {
            if (!opts.LocalLoginEnabled) // local login disabled (SSO enforced)
                return Results.Redirect("/admin/login?error=1");

            var form = await ctx.Request.ReadFormAsync();
            var username = form["username"].ToString();
            var password = form["password"].ToString();

            var user = await users.FindByUsernameAsync(username);
            if (user is null || !users.VerifyPassword(user, password)) // VerifyPassword rejects disabled users
                return Results.Redirect("/admin/login?error=1");

            await SignInAsync(ctx, user);
            return Results.Redirect("/admin/pages");
        }).AllowAnonymous().DisableAntiforgery();

        routes.MapPost("/admin/auth/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CmsAuthSchemes.Cookie);
            return Results.Redirect("/admin/login");
        }).DisableAntiforgery();

        // ── External (SSO) ────────────────────────────────────────────
        // Challenge a registered external scheme; it signs into the temporary external cookie
        // and returns to the callback below.
        routes.MapGet("/admin/auth/external/{scheme}", (string scheme, CmsOptions opts) =>
        {
            if (opts.ExternalLogins.All(p => p.Scheme != scheme))
                return Results.NotFound();

            var props = new AuthenticationProperties { RedirectUri = "/admin/auth/external-callback" };
            props.Items["scheme"] = scheme; // preserved so the callback knows the provider
            return Results.Challenge(props, [scheme]);
        }).AllowAnonymous();

        routes.MapGet("/admin/auth/external-callback", async (HttpContext ctx, UserService users, CmsOptions opts) =>
        {
            var result = await ctx.AuthenticateAsync(CmsAuthSchemes.External);
            if (!result.Succeeded || result.Principal is null)
                return Results.Redirect("/admin/login?error=sso");

            var scheme = result.Properties?.Items.TryGetValue("scheme", out var s) == true && s is not null
                ? s
                : result.Ticket?.AuthenticationScheme ?? CmsAuthSchemes.External;

            var info = (opts.MapExternalUser ?? ExternalUserMapping.Default)(result.Principal);
            if (string.IsNullOrWhiteSpace(info.ExternalId))
                return Results.Redirect("/admin/login?error=sso");

            var user = await users.ProvisionExternalAsync(scheme, info, opts.AutoProvisionExternalUsers);

            // Clear the temporary external cookie regardless of outcome.
            await ctx.SignOutAsync(CmsAuthSchemes.External);

            if (user is null) // unknown identity + auto-provision off, or the matched account is disabled
                return Results.Redirect("/admin/login?error=sso");

            await SignInAsync(ctx, user);
            return Results.Redirect("/admin/pages");
        }).AllowAnonymous();
    }

    /// <summary>Signs the CMS user into the primary admin cookie.</summary>
    private static Task SignInAsync(HttpContext ctx, UserRecord user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
        ], CmsAuthSchemes.Cookie);

        return ctx.SignInAsync(CmsAuthSchemes.Cookie, new ClaimsPrincipal(identity));
    }
}
