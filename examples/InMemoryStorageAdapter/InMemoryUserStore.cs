using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;

namespace Klassd.Examples.InMemoryStorage;

/// <summary>
/// <see cref="IUserStore"/> — backoffice accounts. Lookups by username, id, email and external
/// (SSO) identity all flow through here; the engine handles password hashing and SSO provisioning.
/// </summary>
public sealed class InMemoryUserStore(InMemoryDatabase db) : IUserStore
{
    public Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken ct = default) =>
        Task.FromResult(db.Users.Values
            .FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase))?.Clone());

    public Task<UserRecord?> GetByIdAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(db.Users.TryGetValue(id, out var u) ? u.Clone() : null);

    public Task<IReadOnlyList<UserRecord>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<UserRecord>>(db.Users.Values.Select(u => u.Clone()).ToList());

    public Task InsertAsync(UserRecord user, CancellationToken ct = default)
    {
        db.Users[user.Id] = user.Clone();
        return Task.CompletedTask;
    }

    public Task<UserRecord?> FindByExternalAsync(string provider, string externalId, CancellationToken ct = default) =>
        Task.FromResult(db.Users.Values
            .FirstOrDefault(u => u.Provider == provider && u.ExternalId == externalId)?.Clone());

    public Task<UserRecord?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        Task.FromResult(db.Users.Values
            .FirstOrDefault(u => u.Email is not null && string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase))?.Clone());

    public Task UpdateAsync(UserRecord user, CancellationToken ct = default)
    {
        // Replace mutable fields; in this model we just store the cloned record under its id.
        if (db.Users.ContainsKey(user.Id))
            db.Users[user.Id] = user.Clone();
        return Task.CompletedTask;
    }
}

/// <summary><see cref="IPreferencesStore"/> — per-user UI preferences (selected locale, collapsed nodes).</summary>
public sealed class InMemoryPreferencesStore(InMemoryDatabase db) : IPreferencesStore
{
    public Task<UserPreferencesRecord?> GetAsync(string userId, CancellationToken ct = default) =>
        Task.FromResult(db.Preferences.TryGetValue(userId, out var p) ? p.Clone() : null);

    public Task UpsertAsync(UserPreferencesRecord prefs, CancellationToken ct = default)
    {
        db.Preferences[prefs.UserId] = prefs.Clone();
        return Task.CompletedTask;
    }
}
