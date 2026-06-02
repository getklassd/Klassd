using Klassd.Abstractions.Records;
using Klassd.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Klassd.IntegrationTests;

public class UserAndPreferencesStoreTests
{
    [Test]
    public async Task UserStore_insert_and_lookups()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await using var scope = harness.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserStore>();

        var alice = new UserRecord { Id = Guid.NewGuid().ToString(), Username = "alice", PasswordHash = "h1" };
        var bob = new UserRecord { Id = Guid.NewGuid().ToString(), Username = "bob", PasswordHash = "h2" };
        await users.InsertAsync(alice);
        await users.InsertAsync(bob);

        var byName = await users.FindByUsernameAsync("alice");
        await Assert.That(byName).IsNotNull();
        await Assert.That(byName!.Id).IsEqualTo(alice.Id);
        await Assert.That(byName.PasswordHash).IsEqualTo("h1");

        var byId = await users.GetByIdAsync(bob.Id);
        await Assert.That(byId).IsNotNull();
        await Assert.That(byId!.Username).IsEqualTo("bob");

        var all = await users.GetAllAsync();
        await Assert.That(all).Count().IsEqualTo(2);

        var missing = await users.FindByUsernameAsync("nobody");
        await Assert.That(missing).IsNull();
    }

    [Test]
    public async Task UserStore_external_fields_lookups_and_update()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        await using var scope = harness.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserStore>();

        var sso = new UserRecord
        {
            Id = Guid.NewGuid().ToString(),
            Username = "carol",
            Email = "carol@corp.com",
            PasswordHash = "",          // external user, no password
            Provider = "oidc",
            ExternalId = "sub-carol",
        };
        await users.InsertAsync(sso);

        // New fields round-trip through the TEXT/INTEGER columns.
        var fetched = await users.GetByIdAsync(sso.Id);
        await Assert.That(fetched!.Email).IsEqualTo("carol@corp.com");
        await Assert.That(fetched.Provider).IsEqualTo("oidc");
        await Assert.That(fetched.ExternalId).IsEqualTo("sub-carol");
        await Assert.That(fetched.Disabled).IsFalse();

        // Lookups by external identity + email.
        await Assert.That((await users.FindByExternalAsync("oidc", "sub-carol"))!.Id).IsEqualTo(sso.Id);
        await Assert.That(await users.FindByExternalAsync("oidc", "nope")).IsNull();
        await Assert.That((await users.FindByEmailAsync("carol@corp.com"))!.Id).IsEqualTo(sso.Id);

        // A local user with NULL email/external id round-trips too.
        var local = new UserRecord { Id = Guid.NewGuid().ToString(), Username = "dave", PasswordHash = "h" };
        await users.InsertAsync(local);
        var localFetched = await users.GetByIdAsync(local.Id);
        await Assert.That(localFetched!.Email).IsNull();
        await Assert.That(localFetched.ExternalId).IsNull();
        await Assert.That(localFetched.Provider).IsEqualTo("local");

        // UpdateAsync persists disable + password reset.
        fetched.Disabled = true;
        fetched.PasswordHash = "reset";
        await users.UpdateAsync(fetched);
        var updated = await users.GetByIdAsync(sso.Id);
        await Assert.That(updated!.Disabled).IsTrue();
        await Assert.That(updated.PasswordHash).IsEqualTo("reset");
    }

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
        });

        var stored = await prefs.GetAsync(userId);
        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.SelectedLocale).IsEqualTo("en");
        await Assert.That(stored.Collapsed).Count().IsEqualTo(2);
        await Assert.That(stored.Collapsed.Contains("node-1")).IsTrue();

        // Upsert again updates (SelectedLocale + Collapsed round-trip via jsonb).
        await prefs.UpsertAsync(new UserPreferencesRecord
        {
            UserId = userId,
            SelectedLocale = "da",
            Collapsed = new List<string> { "node-3" },
        });

        var updated = await prefs.GetAsync(userId);
        await Assert.That(updated).IsNotNull();
        await Assert.That(updated!.SelectedLocale).IsEqualTo("da");
        await Assert.That(updated.Collapsed).Count().IsEqualTo(1);
        await Assert.That(updated.Collapsed.Single()).IsEqualTo("node-3");
    }
}
