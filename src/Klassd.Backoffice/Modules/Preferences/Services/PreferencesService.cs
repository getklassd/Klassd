using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Klassd.Backoffice.Modules.Preferences.Models;

namespace Klassd.Backoffice.Modules.Preferences.Services;

public class PreferencesService(IPreferencesStore store)
{
    public async Task<UserPreferencesRecord?> GetAsync(string userId) =>
        await store.GetAsync(userId);

    public async Task<UserPreferencesRecord> UpsertAsync(string userId, UpdatePreferencesRequest update)
    {
        var prefs = await store.GetAsync(userId) ?? new UserPreferencesRecord { UserId = userId };
        if (update.SelectedLocale is not null) prefs.SelectedLocale = update.SelectedLocale;
        if (update.Collapsed is not null) prefs.Collapsed = update.Collapsed;
        if (update.Theme is not null) prefs.Theme = update.Theme;

        await store.UpsertAsync(prefs);
        return prefs;
    }
}
