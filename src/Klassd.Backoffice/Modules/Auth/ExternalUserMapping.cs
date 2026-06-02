using System.Security.Claims;

namespace Klassd.Backoffice.Modules.Auth;

/// <summary>Default claims → <see cref="ExternalUserInfo"/> mapping, used when the host doesn't supply its own.</summary>
public static class ExternalUserMapping
{
    public static ExternalUserInfo Default(ClaimsPrincipal principal)
    {
        var externalId =
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value
            ?? principal.FindFirst("oid")?.Value
            ?? string.Empty;

        var email =
            principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst("email")?.Value;

        var username =
            principal.FindFirst("preferred_username")?.Value
            ?? email
            ?? principal.FindFirst(ClaimTypes.Name)?.Value
            ?? principal.FindFirst("name")?.Value
            ?? externalId;

        return new ExternalUserInfo(externalId, username, email);
    }
}
