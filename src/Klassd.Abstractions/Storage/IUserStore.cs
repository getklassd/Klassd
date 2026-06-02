using Klassd.Abstractions.Records;

namespace Klassd.Abstractions.Storage;

public interface IUserStore
{
    Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken ct = default);
    Task<UserRecord?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<UserRecord>> GetAllAsync(CancellationToken ct = default);
    Task InsertAsync(UserRecord user, CancellationToken ct = default);

    /// <summary>Looks up a user by external (SSO) identity. Null if no account is linked.</summary>
    Task<UserRecord?> FindByExternalAsync(string provider, string externalId, CancellationToken ct = default);

    /// <summary>Looks up a user by email (used to link an SSO identity to an existing account). Null if none.</summary>
    Task<UserRecord?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Replaces an existing user's mutable fields (email, password hash, provider link, disabled).</summary>
    Task UpdateAsync(UserRecord user, CancellationToken ct = default);
}

public interface IPreferencesStore
{
    Task<UserPreferencesRecord?> GetAsync(string userId, CancellationToken ct = default);
    Task UpsertAsync(UserPreferencesRecord prefs, CancellationToken ct = default);
}
