using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Klassd.Backoffice.State;

/// <summary>Resolves the current admin user's id/name from the circuit's auth state.</summary>
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
}
