using Klassd.Abstractions.Records;

namespace Klassd.Abstractions.Storage;

public interface IPreferencesStore
{
    Task<UserPreferencesRecord?> GetAsync(string userId, CancellationToken ct = default);
    Task UpsertAsync(UserPreferencesRecord prefs, CancellationToken ct = default);
}
