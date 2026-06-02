using Klassd.Abstractions.Records;
using Klassd.Backoffice.Modules.Pages.Services;
using Klassd.Backoffice.Modules.Preferences.Models;
using Klassd.Backoffice.Modules.Preferences.Services;

namespace Klassd.Backoffice.State;

/// <summary>A page plus its place in the tree (depth + children). Built per render from the flat list.</summary>
public sealed class PageNode
{
    public required PageRecord Page { get; init; }
    public List<PageNode> Children { get; } = [];
    public int Depth { get; set; }
    public bool HasChildren => Children.Count > 0;
}

/// <summary>Page list + tree/collapse state for the current locale (mirrors usePages).</summary>
public sealed class PageTreeState(
    PageService pages, PreferencesService prefs, AdminUser user, LocaleState locale)
{
    private bool _collapsedLoaded;
    private HashSet<string> _collapsed = [];

    public IReadOnlyList<PageRecord> Pages { get; private set; } = [];
    public IReadOnlyList<PageRecord> PrimaryPages { get; private set; } = [];

    public event Action? Changed;

    public async Task LoadAsync()
    {
        await locale.EnsureLoadedAsync();

        if (!_collapsedLoaded)
        {
            _collapsedLoaded = true;
            var userId = await user.GetUserIdAsync();
            var saved = userId is null ? null : await prefs.GetAsync(userId);
            _collapsed = saved?.Collapsed.ToHashSet() ?? [];
        }

        var selected = locale.SelectedLocale;
        Pages = await pages.GetByLocaleAsync(selected);

        var primaryCode = locale.PrimaryLocale?.Code;
        PrimaryPages = primaryCode is not null && primaryCode != selected
            ? await pages.GetByLocaleAsync(primaryCode)
            : Pages;

        Changed?.Invoke();
    }

    // ── Tree building / flattening ────────────────────────────────────

    public IReadOnlyList<PageNode> FlatTree() => Flatten(BuildTree(Pages));
    public IReadOnlyList<PageNode> FlatPrimaryTree() => Flatten(BuildTree(PrimaryPages));

    private static List<PageNode> BuildTree(IReadOnlyList<PageRecord> records)
    {
        var byId = records.ToDictionary(r => r.Id, r => new PageNode { Page = r });
        var roots = new List<PageNode>();
        foreach (var node in byId.Values)
        {
            if (node.Page.ParentId is { } pid && byId.TryGetValue(pid, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }
        return roots;
    }

    private List<PageNode> Flatten(List<PageNode> roots)
    {
        var flat = new List<PageNode>();
        void Walk(IEnumerable<PageNode> nodes, int depth)
        {
            foreach (var node in nodes.OrderBy(n => n.Page.Slug, StringComparer.OrdinalIgnoreCase))
            {
                node.Depth = depth;
                flat.Add(node);
                if (node.HasChildren && !_collapsed.Contains(node.Page.Id))
                    Walk(node.Children, depth + 1);
            }
        }
        Walk(roots, 0);
        return flat;
    }

    // ── Collapse ──────────────────────────────────────────────────────

    public bool IsCollapsed(string id) => _collapsed.Contains(id);

    public async Task ToggleCollapseAsync(string id)
    {
        if (!_collapsed.Add(id)) _collapsed.Remove(id);
        Changed?.Invoke();
        var userId = await user.GetUserIdAsync();
        if (userId is not null)
            await prefs.UpsertAsync(userId, new UpdatePreferencesRequest(Collapsed: _collapsed.ToList()));
    }

    // ── Slug / counts ─────────────────────────────────────────────────

    /// <summary>Public URL slug incl. locale prefix for non-primary locales.</summary>
    public string GetFullSlug(PageRecord page)
    {
        var prefix = locale.IsPrimary ? "" : locale.SelectedLocale + "/";
        return string.IsNullOrEmpty(page.Slug)
            ? "/" + prefix.TrimEnd('/')
            : "/" + prefix + page.Slug;
    }

    public bool HasTranslation(string contentId) =>
        Pages.Any(p => p.ContentId == contentId);

    public PageRecord? Translation(string contentId) =>
        Pages.FirstOrDefault(p => p.ContentId == contentId);

    public string PageCountText =>
        locale.IsPrimary
            ? $"{Pages.Count} page{(Pages.Count == 1 ? "" : "s")}"
            : $"{PrimaryPages.Count} page{(PrimaryPages.Count == 1 ? "" : "s")} · {Pages.Count} translated";
}
