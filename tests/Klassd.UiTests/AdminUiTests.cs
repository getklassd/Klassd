using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TUnit.Core;
using TUnit.Playwright;

namespace Klassd.UiTests;

/// <summary>
/// End-to-end browser tests for the Blazor admin (TUnit + Playwright).
/// Requires Playwright browsers — after building, run once:
///   pwsh tests/Klassd.UiTests/bin/Debug/net9.0/playwright.ps1 install chromium
/// then `dotnet test` (or `dotnet run --project tests/Klassd.UiTests`).
/// </summary>
public class AdminUiTests : PageTest
{
    private static string Url(string path) => GlobalHooks.BaseUrl + path;

    private async Task LoginAsync()
    {
        await Page.GotoAsync(Url("/admin/login"));
        await Page.FillAsync("input[name='username']", "admin");
        await Page.FillAsync("input[name='password']", "admin");
        await Page.ClickAsync("button:has-text('Sign in')");
        await Page.WaitForURLAsync(new Regex(@"/admin/pages"));
    }

    /// <summary>
    /// Opens the create panel. PagesPage is interactive-server, so right after a full
    /// navigation the SignalR circuit may not be wired yet and the first click is a
    /// no-op — retry until the panel actually opens.
    /// </summary>
    private async Task OpenCreatePanelAsync()
    {
        var panel = Page.Locator(".panel.open");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await Page.ClickAsync("button:has-text('New Page')");
            try
            {
                await panel.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 1500,
                });
                return;
            }
            catch (TimeoutException) { /* circuit not interactive yet; retry */ }
        }
        throw new TimeoutException("Editor panel did not open (Blazor circuit not interactive?).");
    }

    [Test]
    public async Task Unauthenticated_admin_redirects_to_login()
    {
        await Page.GotoAsync(Url("/admin"));
        await Expect(Page).ToHaveURLAsync(new Regex(@"/admin/login"));
        await Expect(Page.Locator(".login-card")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_with_seeded_admin_succeeds()
    {
        await LoginAsync();
        await Expect(Page.Locator("h1")).ToHaveTextAsync("Pages");
    }

    [Test]
    public async Task Login_with_wrong_password_shows_error()
    {
        await Page.GotoAsync(Url("/admin/login"));
        await Page.FillAsync("input[name='username']", "admin");
        await Page.FillAsync("input[name='password']", "wrong");
        await Page.ClickAsync("button:has-text('Sign in')");
        await Expect(Page.Locator(".login-error")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Can_create_a_page()
    {
        await LoginAsync();

        var name = "UI Test Page " + Guid.NewGuid().ToString("N")[..6];

        await OpenCreatePanelAsync();

        // Page Type (first select in the edit column)
        await Page.Locator(".edit-col .form-group select").First.SelectOptionAsync(new SelectOptionValue { Value = "ContentPage" });
        // Name (first text input in the edit column; slug is the second)
        await Page.Locator(".edit-col input[type='text']").First.FillAsync(name);
        await Page.ClickAsync("button:has-text('Save Page')");

        // Panel closes; the new page appears in the tree.
        await Expect(Page.Locator(".panel.open")).Not.ToBeVisibleAsync();
        await Expect(Page.GetByText(name)).ToBeVisibleAsync();
    }

    [Test]
    public async Task Custom_color_editor_renders_for_hero_block()
    {
        await LoginAsync();

        await OpenCreatePanelAsync();
        await Page.Locator(".edit-col .form-group select").First.SelectOptionAsync(new SelectOptionValue { Value = "HomePage" });

        // "Hero Blocks" area → + Add Block → choose HeroBlock.
        await Page.Locator(".blocks-header:has(label:has-text('Hero Blocks')) button:has-text('Add Block')").ClickAsync();
        await Page.Locator(".add-block-form select").SelectOptionAsync(new SelectOptionValue { Value = "HeroBlock" });

        // HeroBlock.BackgroundColor uses the consumer's [PropertyEditor("color")] component —
        // a native colour input proves the custom editor resolved (no JS, no registration).
        await Expect(Page.Locator(".add-block-form input[type='color']")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Media_library_loads_with_configured_sections()
    {
        await LoginAsync();
        await Page.GotoAsync(Url("/admin/media"));

        // Sample declares FileSystem media sections, so the library (not the empty state) renders.
        await Expect(Page.Locator("h1")).ToHaveTextAsync("Media");
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Upload" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Can_create_a_user()
    {
        await LoginAsync();
        await Page.GotoAsync(Url("/admin/users"));

        // UsersPage is interactive-server; the first click can race the SignalR circuit.
        var usernameInput = Page.GetByPlaceholder("jane", new() { Exact = true });
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await Page.ClickAsync("button:has-text('New user')");
            try
            {
                await usernameInput.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 1500,
                });
                break;
            }
            catch (TimeoutException) { /* circuit not interactive yet; retry */ }
        }

        var name = "uitest_" + Guid.NewGuid().ToString("N")[..6];
        await usernameInput.FillAsync(name);
        await Page.Locator(".card:has(.card-header:has-text('New user')) input[type='password']").FillAsync("pw-12345");
        await Page.ClickAsync("button:has-text('Create')");

        // The new user appears in the list.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = name })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Content_delivery_is_anonymous_but_management_is_protected()
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

        // Public delivery — no auth required.
        var dict = await http.GetAsync(Url("/api/dictionary/resolved/en"));
        await Assert.That((int)dict.StatusCode).IsEqualTo(200);

        var pages = await http.GetAsync(Url("/api/pages?locale=en"));
        await Assert.That((int)pages.StatusCode).IsEqualTo(200);

        // Management endpoint stays protected → cookie challenge (redirect to login), not 200.
        var users = await http.GetAsync(Url("/api/users"));
        await Assert.That((int)users.StatusCode).IsNotEqualTo(200);
    }

    [Test]
    public async Task Can_create_a_dictionary_key()
    {
        await LoginAsync();
        await Page.GotoAsync(Url("/admin/dictionary"));

        // DictionaryPage is interactive-server; the first click can race the SignalR circuit.
        var keyInput = Page.GetByPlaceholder("common.no");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await Page.ClickAsync("button:has-text('New key')");
            try
            {
                await keyInput.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 1500 });
                break;
            }
            catch (TimeoutException) { /* circuit not interactive yet; retry */ }
        }

        var key = "uitest." + Guid.NewGuid().ToString("N")[..6];
        await keyInput.FillAsync(key);
        await Page.ClickAsync("button:has-text('Create')");

        // The new key appears in the table.
        await Expect(Page.GetByText(key)).ToBeVisibleAsync();
    }

    [Test]
    public async Task Media_picker_editor_renders_for_hero_block()
    {
        await LoginAsync();

        await OpenCreatePanelAsync();
        await Page.Locator(".edit-col .form-group select").First.SelectOptionAsync(new SelectOptionValue { Value = "HomePage" });

        await Page.Locator(".blocks-header:has(label:has-text('Hero Blocks')) button:has-text('Add Block')").ClickAsync();
        await Page.Locator(".add-block-form select").SelectOptionAsync(new SelectOptionValue { Value = "HeroBlock" });

        // HeroBlock.Image uses FieldType="media" → the engine's MediaPickerEditor resolves via
        // FieldEditor's default mapping (no consumer registration).
        await Expect(Page.Locator(".add-block-form .media-picker button:has-text('Pick media')")).ToBeVisibleAsync();
    }
}
