namespace Klassd.Abstractions.Records;

public sealed class UserRecord
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;

    /// <summary>Optional email; the join key when linking an external (SSO) identity to an account.</summary>
    public string? Email { get; set; }

    /// <summary>PBKDF2 hash for local users; empty for external-only (SSO) users.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Login provider: <c>"local"</c> or an external scheme name (e.g. <c>"oidc"</c>, <c>"saml"</c>).</summary>
    public string Provider { get; set; } = "local";

    /// <summary>Stable subject from the identity provider (OIDC <c>sub</c> / SAML NameID); null for local users.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Disabled users cannot sign in (kept rather than deleted so authored content keeps its author).</summary>
    public bool Disabled { get; set; }
}

public sealed class UserPreferencesRecord
{
    public string UserId { get; set; } = string.Empty;
    public string SelectedLocale { get; set; } = string.Empty;
    public List<string> Collapsed { get; set; } = new();
}
