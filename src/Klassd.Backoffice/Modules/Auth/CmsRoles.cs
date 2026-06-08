namespace Klassd.Backoffice.Modules.Auth;

/// <summary>Backoffice capabilities. Roles grant a union of these; endpoints/UI gate on them.</summary>
[Flags]
public enum Capabilities
{
    None             = 0,
    PagesEdit        = 1 << 0,  // create/edit pages and save drafts
    PagesPublish     = 1 << 1,  // publish/unpublish pages
    MediaManage      = 1 << 2,
    DictionaryManage = 1 << 3,
    GlobalsEdit      = 1 << 4,
    UsersManage      = 1 << 5,
    SettingsManage   = 1 << 6,
    All = PagesEdit | PagesPublish | MediaManage | DictionaryManage | GlobalsEdit | UsersManage | SettingsManage,
}

/// <summary>
/// Built-in roles and their capability sets. A user may hold several roles; effective capabilities
/// are the union. A user with no roles is treated as <see cref="Administrator"/> for back-compat
/// (pre-roles installs had no roles and every user was an admin).
/// </summary>
public static class CmsRoles
{
    public const string Administrator = "Administrator";
    public const string Editor = "Editor";   // edit + publish content; no user/settings admin
    public const string Author = "Author";    // edit + save drafts, but cannot publish

    private static readonly Dictionary<string, Capabilities> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [Administrator] = Capabilities.All,
        [Editor] = Capabilities.PagesEdit | Capabilities.PagesPublish | Capabilities.MediaManage
                 | Capabilities.DictionaryManage | Capabilities.GlobalsEdit,
        [Author] = Capabilities.PagesEdit | Capabilities.MediaManage,
    };

    public static IReadOnlyList<string> All => [Administrator, Editor, Author];

    public static bool IsKnown(string role) => Map.ContainsKey(role);

    /// <summary>Union of capabilities across the roles. Null/empty ⇒ <see cref="Capabilities.All"/> (back-compat).</summary>
    public static Capabilities Resolve(IEnumerable<string>? roles)
    {
        if (roles is null) return Capabilities.All;
        var caps = Capabilities.None;
        var any = false;
        foreach (var role in roles)
        {
            any = true;
            if (Map.TryGetValue(role, out var c)) caps |= c;
        }
        return any ? caps : Capabilities.All;
    }
}
