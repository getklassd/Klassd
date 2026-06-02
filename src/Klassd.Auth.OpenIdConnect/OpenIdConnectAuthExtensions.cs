using Klassd.Abstractions;
using Klassd.Backoffice.Modules.Auth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Auth.OpenIdConnect;

/// <summary>
/// Adds OpenID Connect / OAuth 2.0 single sign-on to the admin login. Built on the engine's
/// external-login seam: the handler signs into the temporary external cookie and the CMS
/// provisions/links a user from its claims.
/// <code>
/// builder.Services.AddKlassd(cfg).UseSqlite(cfg.GetSection("Sqlite"))
///     .UseOpenIdConnect("Company SSO", cfg.GetSection("Oidc"));
/// </code>
/// </summary>
public static class OpenIdConnectAuthExtensions
{
    /// <summary>Registers an OIDC provider, configured by <paramref name="configure"/>.</summary>
    public static ICmsBuilder UseOpenIdConnect(
        this ICmsBuilder cms, string displayName, Action<OpenIdConnectOptions> configure, string scheme = "oidc")
    {
        ArgumentNullException.ThrowIfNull(configure);
        return cms.AddExternalLogin(scheme, displayName, auth =>
            auth.AddOpenIdConnect(scheme, options =>
            {
                options.SignInScheme = CmsAuthSchemes.External;   // engine exchanges this for the admin cookie
                options.CallbackPath = $"/signin-{scheme}";
                options.GetClaimsFromUserInfoEndpoint = true;
                configure(options);
            }));
    }

    /// <summary>
    /// Registers an OIDC provider from a configuration section (<c>Authority</c>, <c>ClientId</c>,
    /// <c>ClientSecret</c>, optional <c>ResponseType</c>, <c>Scope</c> array, <c>SaveTokens</c>).
    /// </summary>
    public static ICmsBuilder UseOpenIdConnect(
        this ICmsBuilder cms, string displayName, IConfiguration section, string scheme = "oidc")
    {
        ArgumentNullException.ThrowIfNull(section);
        return cms.UseOpenIdConnect(displayName, options =>
        {
            options.Authority = section["Authority"];
            options.ClientId = section["ClientId"];
            options.ClientSecret = section["ClientSecret"];
            options.ResponseType = section["ResponseType"] ?? "code";
            if (bool.TryParse(section["SaveTokens"], out var saveTokens))
                options.SaveTokens = saveTokens;

            var scopes = section.GetSection("Scope").Get<string[]>();
            if (scopes is { Length: > 0 })
            {
                options.Scope.Clear();
                foreach (var s in scopes)
                    options.Scope.Add(s);
            }
        }, scheme);
    }
}
