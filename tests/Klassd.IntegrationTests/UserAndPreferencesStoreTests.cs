using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.IntegrationTests;

// Note: the user store moved to the external Klassd.Auth suite (Klassd.Auth.Data.*), so it is
// covered by that repo's tests; the CMS adapters now only own preferences (plus pages/media/etc).
public class PreferencesStoreTests
{
    [Test]
    public async Task PreferencesStore_upsert_then_get_and_update()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await using var scope = harness.CreateScope();
        var prefs = scope.ServiceProvider.GetRequiredService<IPreferencesStore>();

        var userId = Guid.NewGuid().ToString();

        // Missing -> null.
        await Assert.That(await prefs.GetAsync(userId)).IsNull();

        await prefs.UpsertAsync(new UserPreferencesRecord
        {
            UserId = userId,
            SelectedLocale = "en",
            Collapsed = new List<string> { "node-1", "node-2" },
            Theme = "dark",
        });

        var stored = await prefs.GetAsync(userId);
        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.SelectedLocale).IsEqualTo("en");
        await Assert.That(stored.Collapsed).Count().IsEqualTo(2);
        await Assert.That(stored.Collapsed.Contains("node-1")).IsTrue();
        await Assert.That(stored.Theme).IsEqualTo("dark");

        // Upsert again updates (SelectedLocale + Collapsed round-trip via jsonb; theme round-trips).
        await prefs.UpsertAsync(new UserPreferencesRecord
        {
            UserId = userId,
            SelectedLocale = "da",
            Collapsed = new List<string> { "node-3" },
            Theme = "light",
        });

        var updated = await prefs.GetAsync(userId);
        await Assert.That(updated).IsNotNull();
        await Assert.That(updated!.SelectedLocale).IsEqualTo("da");
        await Assert.That(updated.Collapsed).Count().IsEqualTo(1);
        await Assert.That(updated.Collapsed.Single()).IsEqualTo("node-3");
        await Assert.That(updated.Theme).IsEqualTo("light");
    }
}
