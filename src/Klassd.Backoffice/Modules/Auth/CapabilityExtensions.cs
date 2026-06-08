using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Klassd.Backoffice.Modules.Auth;

/// <summary>Resolves and enforces <see cref="Capabilities"/> from a signed-in user's role claims.</summary>
public static class CapabilityExtensions
{
    /// <summary>The user's effective capabilities (union across role claims; no roles ⇒ All).</summary>
    public static Capabilities Capabilities(this ClaimsPrincipal user) =>
        CmsRoles.Resolve(user.FindAll(ClaimTypes.Role).Select(c => c.Value));

    public static bool HasCapability(this ClaimsPrincipal user, Capabilities capability) =>
        (user.Capabilities() & capability) == capability;

    /// <summary>
    /// Requires the caller to hold <paramref name="capability"/>; otherwise the endpoint returns 403.
    /// Layer on top of the group's <c>RequireAuthorization()</c> (which already enforces 401).
    /// </summary>
    public static RouteHandlerBuilder RequireCapability(this RouteHandlerBuilder builder, Capabilities capability) =>
        builder.AddEndpointFilter(async (ctx, next) =>
            ctx.HttpContext.User.HasCapability(capability)
                ? await next(ctx)
                : Results.Forbid());
}
