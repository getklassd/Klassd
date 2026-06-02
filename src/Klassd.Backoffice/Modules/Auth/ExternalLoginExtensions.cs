using Klassd.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.Backoffice.Modules.Auth;

/// <summary>
/// Host/adapter-facing seam for adding SSO providers to the admin login. SSO packages
/// (e.g. Klassd.Auth.OpenIdConnect / .Saml) build their <c>UseXxx</c> on top of this.
/// </summary>
public static class ExternalLoginExtensions
{
    /// <summary>
    /// Registers an external login provider: records it (so a "Sign in with {displayName}" button
    /// appears on the login page) and lets <paramref name="configure"/> attach the actual handler
    /// (OIDC/SAML/…) to the shared authentication builder. The handler MUST set
    /// <c>SignInScheme = <see cref="CmsAuthSchemes.External"/></c> so the external-login callback can
    /// provision/link the CMS user.
    /// </summary>
    public static ICmsBuilder AddExternalLogin(
        this ICmsBuilder cms, string scheme, string displayName, Action<AuthenticationBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(configure);

        var options = cms.ResolveOptions();
        if (options.ExternalLogins.Any(p => p.Scheme == scheme))
            throw new InvalidOperationException($"An external login with scheme '{scheme}' is already registered.");
        options.ExternalLogins.Add(new ExternalLoginDescriptor(scheme, displayName));

        // AddAuthentication() returns a builder over the same service collection; the cookie
        // schemes were already configured in AddKlassd, so this only adds the new handler.
        configure(cms.Services.AddAuthentication());
        return cms;
    }

    /// <summary>Resolves the singleton <see cref="CmsOptions"/> instance captured by AddKlassd.</summary>
    internal static CmsOptions ResolveOptions(this ICmsBuilder cms)
    {
        var descriptor = cms.Services.LastOrDefault(d => d.ServiceType == typeof(CmsOptions))
            ?? throw new InvalidOperationException(
                "Klassd is not registered. Call AddKlassd() before adding external logins.");
        return (CmsOptions)descriptor.ImplementationInstance!;
    }
}
