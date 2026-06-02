using Klassd.Core.Localization;
using Klassd.Backoffice.Modules.Preferences.Models;
using Klassd.Backoffice.Modules.Preferences.Services;

namespace Klassd.Backoffice.State;

/// <summary>Selected locale + locale metadata (mirrors useLocales). Persists selection to preferences.</summary>
public sealed class LocaleState(LocaleRegistry registry, PreferencesService prefs, AdminUser user)
{
    private bool _loaded;

    public IReadOnlyList<LocaleDefinition> Locales => registry.All;
    public string SelectedLocale { get; private set; } = "";

    public LocaleDefinition? PrimaryLocale => Locales.FirstOrDefault(l => l.Mandatory);
    public LocaleDefinition? DefaultLocale => Locales.FirstOrDefault(l => l.IsDefault) ?? PrimaryLocale;
    public bool IsPrimary => SelectedLocale == PrimaryLocale?.Code;
    public IEnumerable<LocaleDefinition> OtherLocales => Locales.Where(l => l.Code != SelectedLocale);

    public event Action? Changed;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        _loaded = true;

        var userId = await user.GetUserIdAsync();
        var saved = userId is null ? null : await prefs.GetAsync(userId);
        SelectedLocale = !string.IsNullOrEmpty(saved?.SelectedLocale)
            ? saved!.SelectedLocale
            : DefaultLocale?.Code ?? Locales.FirstOrDefault()?.Code ?? "en";
        Changed?.Invoke();
    }

    public async Task SetLocaleAsync(string code)
    {
        if (code == SelectedLocale) return;
        SelectedLocale = code;
        var userId = await user.GetUserIdAsync();
        if (userId is not null)
            await prefs.UpsertAsync(userId, new UpdatePreferencesRequest(SelectedLocale: code));
        Changed?.Invoke();
    }
}
