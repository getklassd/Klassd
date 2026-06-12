namespace Klassd.Abstractions.Records;

public sealed class UserPreferencesRecord
{
    public string UserId { get; set; } = string.Empty;
    public string SelectedLocale { get; set; } = string.Empty;
    public List<string> Collapsed { get; set; } = new();

    /// <summary>Admin UI theme: <c>"light"</c>, <c>"dark"</c>, or empty to follow the OS preference.
    /// Stored server-side so the choice follows the user across machines.</summary>
    public string Theme { get; set; } = string.Empty;
}
