using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace Klassd.UiTests;

/// <summary>
/// Not an assertion test — a capture utility. Logs into the admin, creates a little content so the
/// views aren't empty, and writes PNGs to <c>docs/images/</c> for the README. Run it explicitly:
///   dotnet run --project tests/Klassd.UiTests -c Release -- --treenode-filter "/*/*/ScreenshotCaptureTests/*"
/// (Playwright browsers must be installed first; see README.)
/// </summary>
public class ScreenshotCaptureTests : PageTest
{
    private static string Url(string path) => GlobalHooks.BaseUrl + path;

    private static string OutputDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Klassd.slnx")))
                dir = dir.Parent;
            var root = dir?.FullName ?? throw new DirectoryNotFoundException("Could not locate Klassd.slnx.");
            return Path.Combine(root, "docs", "images");
        }
    }

    private async Task ShotAsync(string fileName, bool fullPage = true)
    {
        Directory.CreateDirectory(OutputDir);
        await Page.WaitForTimeoutAsync(500);   // let Blazor render + animations settle
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(OutputDir, fileName),
            FullPage = fullPage,
        });
    }

    /// <summary>PagesPage is interactive-server; the first click can race the SignalR circuit — retry.</summary>
    private async Task OpenCreatePanelAsync()
    {
        var panel = Page.Locator(".editor-inline");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await Page.ClickAsync("button:has-text('+ New')");
            try
            {
                await panel.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 1500 });
                return;
            }
            catch (TimeoutException) { /* circuit not interactive yet; retry */ }
        }
        throw new TimeoutException("Editor panel did not open.");
    }

    [Test]
    public async Task Capture_admin_screenshots()
    {
        await Page.SetViewportSizeAsync(1440, 900);

        // ── 1. Login screen ───────────────────────────────────────────
        await Page.GotoAsync(Url("/admin/login"));
        await Expect(Page.Locator(".login-card")).ToBeVisibleAsync();
        // The app focuses the main <h1> on navigation (accessibility); that shows a focus outline
        // on the heading. Mouse users never see it, but it's noise in a screenshot — clear focus.
        await Page.EvaluateAsync("() => (document.activeElement && document.activeElement.blur) && document.activeElement.blur()");
        await ShotAsync("01-login.png", fullPage: false);

        // Sign in with the seeded admin account.
        await Page.FillAsync("input[name='username']", "admin");
        await Page.FillAsync("input[name='password']", "admin");
        await Page.ClickAsync("button:has-text('Sign in')");
        await Page.WaitForURLAsync(new Regex(@"/admin/pages"));

        // ── 2. Page editor (create panel: page type, fields, block area) ──
        await OpenCreatePanelAsync();
        await Page.Locator(".edit-col .form-group select").First.SelectOptionAsync(new SelectOptionValue { Value = "HomePage" });
        // Name is the first text input; leave Slug (the second) to auto-generate from the name.
        await Page.Locator(".edit-col input[type='text']").First.FillAsync("Home");
        // Fill the localized Title field by its label rather than by index.
        await Page.Locator(".edit-col .form-group:has(label:has-text('Title')) input[type='text']").First.FillAsync("Welcome to Klassd");

        await Page.Locator(".blocks-header:has(label:has-text('Hero Blocks')) button:has-text('Add Block')").ClickAsync();
        await Page.Locator(".add-block-form select").SelectOptionAsync(new SelectOptionValue { Value = "HeroBlock" });
        await Page.Locator(".add-block-form input[type='text']").First.FillAsync("Code-first content, no compromises");
        await ShotAsync("03-page-editor.png", fullPage: false);

        // Persist so the tree isn't empty for the next shot.
        await Page.ClickAsync("button:has-text('Save Page')");
        await Expect(Page.Locator(".editor-inline")).Not.ToBeVisibleAsync();
        await Expect(Page.Locator(".tree-title:has-text('Home')").First).ToBeVisibleAsync();

        // ── 3. Pages list (top-bar areas + left tree + center detail) ──
        await Expect(Page.Locator(".context-tree-header h2")).ToHaveTextAsync("Pages");
        await ShotAsync("02-pages.png");

        // ── 4. Media library (left section list + grid) ───────────────
        await Page.GotoAsync(Url("/admin/media"));
        await Expect(Page.Locator(".context-tree-header h2")).ToHaveTextAsync("Media");
        await ShotAsync("04-media.png");

        // ── 5. Users ──────────────────────────────────────────────────
        await Page.GotoAsync(Url("/admin/users"));
        await Expect(Page.Locator("h1")).ToHaveTextAsync("Users");
        await ShotAsync("05-users.png");

        // ── 6. Dictionary ─────────────────────────────────────────────
        await Page.GotoAsync(Url("/admin/dictionary"));
        await Expect(Page.Locator("h1")).ToHaveTextAsync("Dictionary");
        await ShotAsync("06-dictionary.png");

        // ── 7. Dark-mode pass ─────────────────────────────────────────
        // Flip the theme (persists to preferences, so it survives navigation) and re-shoot.
        await Page.GotoAsync(Url("/admin/pages"));
        await Page.ClickAsync(".theme-toggle");
        await Page.WaitForFunctionAsync("() => document.documentElement.getAttribute('data-theme') === 'dark'");
        await Expect(Page.Locator(".tree-title:has-text('Home')").First).ToBeVisibleAsync();
        await ShotAsync("02-pages-dark.png");

        await Page.ClickAsync(".tree-title:has-text('Home')");
        await Expect(Page.Locator(".editor-inline")).ToBeVisibleAsync();
        await ShotAsync("03-page-editor-dark.png", fullPage: false);

        await Page.GotoAsync(Url("/admin/media"));
        await Expect(Page.Locator(".context-tree-header h2")).ToHaveTextAsync("Media");
        await ShotAsync("04-media-dark.png");
    }
}
