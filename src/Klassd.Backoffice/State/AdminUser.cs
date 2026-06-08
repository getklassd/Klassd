using System.Security.Claims;
using Klassd.Backoffice.Modules.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace Klassd.Backoffice.State;

/// <summary>Resolves the current admin user's id/name/capabilities from the circuit's auth state.</summary>
public sealed class AdminUser(AuthenticationStateProvider authStateProvider)
{
    public async Task<string?> GetUserIdAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        return state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    public async Task<string?> GetUserNameAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        return state.User.Identity?.Name;
    }

    /// <summary>The current user's effective capabilities (from role claims).</summary>
    public async Task<Capabilities> GetCapabilitiesAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        return state.User.Capabilities();
    }

    public async Task<bool> HasCapabilityAsync(Capabilities capability) =>
        (await GetCapabilitiesAsync()).HasFlag(capability);
}
