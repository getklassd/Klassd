using Microsoft.AspNetCore.Authentication.Cookies;

namespace Klassd.Backoffice.Modules.Auth;

/// <summary>Authentication scheme names used by the admin.</summary>
public static class CmsAuthSchemes
{
    /// <summary>Primary cookie the admin is authenticated with.</summary>
    public const string Cookie = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// Temporary cookie that external (SSO) handlers sign into. The external-login callback
    /// reads it, provisions/links a CMS user, then signs into <see cref="Cookie"/> and clears it.
    /// External handlers (OIDC/SAML) must set <c>SignInScheme = CmsAuthSchemes.External</c>.
    /// </summary>
    public const string External = "cms_external";
}

/// <summary>A configured external login provider, surfaced as a button on the login page.</summary>
public sealed record ExternalLoginDescriptor(string Scheme, string DisplayName);

/// <summary>The identity extracted from an external provider's claims.</summary>
public sealed record ExternalUserInfo(string ExternalId, string Username, string? Email);
