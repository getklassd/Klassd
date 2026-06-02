namespace Klassd.Backoffice.Modules.Preferences.Models;

public record UpdatePreferencesRequest(string? SelectedLocale = null, List<string>? Collapsed = null, string? Theme = null);
