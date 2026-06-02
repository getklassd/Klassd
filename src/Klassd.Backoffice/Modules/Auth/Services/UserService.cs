using System.Security.Cryptography;
using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;

namespace Klassd.Backoffice.Modules.Auth.Services;

public class UserService(IUserStore store)
{
    public async Task<UserRecord?> FindByUsernameAsync(string username) =>
        await store.FindByUsernameAsync(username);

    public async Task<UserRecord?> GetByIdAsync(string id) =>
        await store.GetByIdAsync(id);

    public async Task<IReadOnlyList<UserRecord>> GetAllAsync() =>
        await store.GetAllAsync();

    /// <summary>Creates a local (password) user. Throws if the username is already taken.</summary>
    public async Task<UserRecord> CreateAsync(string username, string password, string? email = null)
    {
        username = username.Trim();
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");
        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Password is required.");
        if (await store.FindByUsernameAsync(username) is not null)
            throw new InvalidOperationException($"A user named '{username}' already exists.");

        var user = new UserRecord
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            PasswordHash = HashPassword(password),
            Provider = "local",
        };
        await store.InsertAsync(user);
        return user;
    }

    /// <summary>Enables/disables a user. Disabled users cannot sign in (local or SSO).</summary>
    public async Task<bool> SetDisabledAsync(string id, bool disabled)
    {
        var user = await store.GetByIdAsync(id);
        if (user is null) return false;
        user.Disabled = disabled;
        await store.UpdateAsync(user);
        return true;
    }

    /// <summary>Resets a user's password (and makes them a local user if they weren't).</summary>
    public async Task<bool> ResetPasswordAsync(string id, string newPassword)
    {
        if (string.IsNullOrEmpty(newPassword))
            throw new InvalidOperationException("Password is required.");
        var user = await store.GetByIdAsync(id);
        if (user is null) return false;
        user.PasswordHash = HashPassword(newPassword);
        await store.UpdateAsync(user);
        return true;
    }

    /// <summary>
    /// Find-or-link-or-create a CMS user for an external (SSO) identity. Returns the user, or
    /// null when the matched account is disabled, or when unknown and auto-provisioning is off.
    /// </summary>
    public async Task<UserRecord?> ProvisionExternalAsync(string provider, ExternalUserInfo info, bool autoProvision)
    {
        // 1) Already linked by (provider, external id).
        var linked = await store.FindByExternalAsync(provider, info.ExternalId);
        if (linked is not null) return linked.Disabled ? null : linked;

        // 2) Link to an existing account that shares the email.
        if (!string.IsNullOrWhiteSpace(info.Email))
        {
            var byEmail = await store.FindByEmailAsync(info.Email);
            if (byEmail is not null)
            {
                if (byEmail.Disabled) return null;
                byEmail.Provider = provider;
                byEmail.ExternalId = info.ExternalId;
                await store.UpdateAsync(byEmail);
                return byEmail;
            }
        }

        // 3) Auto-provision a new external user.
        if (!autoProvision) return null;
        var user = new UserRecord
        {
            Id = Guid.NewGuid().ToString(),
            Username = string.IsNullOrWhiteSpace(info.Username) ? info.ExternalId : info.Username,
            Email = string.IsNullOrWhiteSpace(info.Email) ? null : info.Email,
            Provider = provider,
            ExternalId = info.ExternalId,
            PasswordHash = string.Empty,
        };
        await store.InsertAsync(user);
        return user;
    }

    public async Task SeedAdminAsync()
    {
        var existing = await FindByUsernameAsync("admin");
        if (existing is null)
            await CreateAsync("admin", "admin");
    }

    /// <summary>True only for enabled local users with a matching password (external users have no password).</summary>
    public bool VerifyPassword(UserRecord user, string password) =>
        !user.Disabled && !string.IsNullOrEmpty(user.PasswordHash) && VerifyPasswordHash(password, user.PasswordHash);

    // ── PBKDF2-SHA256, no extra packages ──────────────────────────────────────
    private static string HashPassword(string password)
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        var hash = Pbkdf2(password, salt);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPasswordHash(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = Pbkdf2(password, salt);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Pbkdf2(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
}
