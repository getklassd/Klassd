using Klassd.Backoffice;
using Klassd.Backoffice.Modules.Auth;
using Klassd.Backoffice.Modules.Auth.Services;

namespace Klassd.UnitTests;

public class LocalLoginPolicyTests
{
    [Test]
    public async Task Local_login_enabled_by_default()
    {
        await Assert.That(new CmsOptions().LocalLoginEnabled).IsTrue();
    }

    [Test]
    public async Task Disabling_local_login_has_no_effect_without_an_sso_provider()
    {
        // Safety: can't lock everyone out when there's no SSO to fall back to.
        var opts = new CmsOptions { AllowLocalLogin = false };
        await Assert.That(opts.LocalLoginEnabled).IsTrue();
    }

    [Test]
    public async Task Disabling_local_login_takes_effect_once_an_sso_provider_exists()
    {
        var opts = new CmsOptions { AllowLocalLogin = false };
        AddProvider(opts, "oidc", "Company SSO");
        await Assert.That(opts.LocalLoginEnabled).IsFalse();
    }

    [Test]
    public async Task Local_login_stays_enabled_with_a_provider_when_allowed()
    {
        var opts = new CmsOptions(); // AllowLocalLogin = true
        AddProvider(opts, "oidc", "Company SSO");
        await Assert.That(opts.LocalLoginEnabled).IsTrue();
    }

    // ExternalLogins is internal (added via AddExternalLogin); reach it for the policy test.
    private static void AddProvider(CmsOptions opts, string scheme, string name)
    {
        var prop = typeof(CmsOptions).GetProperty("ExternalLogins",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var list = (System.Collections.IList)prop.GetValue(opts)!;
        list.Add(new ExternalLoginDescriptor(scheme, name));
    }
}

public class UserServiceTests
{
    [Test]
    public async Task CreateAsync_assigns_guid_id_and_verifiable_password()
    {
        var svc = new UserService(new InMemoryUserStore());
        var user = await svc.CreateAsync("alice", "s3cret");

        await Assert.That(Guid.TryParse(user.Id, out _)).IsTrue();
        await Assert.That(svc.VerifyPassword(user, "s3cret")).IsTrue();
        await Assert.That(svc.VerifyPassword(user, "wrong")).IsFalse();
    }

    [Test]
    public async Task Same_password_produces_different_hashes_per_user()
    {
        var svc = new UserService(new InMemoryUserStore());
        var a = await svc.CreateAsync("a", "pw");
        var b = await svc.CreateAsync("b", "pw");

        await Assert.That(a.PasswordHash).IsNotEqualTo(b.PasswordHash);
        // Both still verify against the shared password.
        await Assert.That(svc.VerifyPassword(a, "pw")).IsTrue();
        await Assert.That(svc.VerifyPassword(b, "pw")).IsTrue();
    }

    [Test]
    public async Task FindByUsernameAsync_returns_created_user()
    {
        var svc = new UserService(new InMemoryUserStore());
        await svc.CreateAsync("bob", "pw");

        var found = await svc.FindByUsernameAsync("bob");
        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Username).IsEqualTo("bob");
    }

    [Test]
    public async Task SeedAdminAsync_creates_admin_once()
    {
        var store = new InMemoryUserStore();
        var svc = new UserService(store);

        await svc.SeedAdminAsync();
        await svc.SeedAdminAsync(); // idempotent

        var admins = store.Users.Where(u => u.Username == "admin").ToList();
        await Assert.That(admins).Count().IsEqualTo(1);
        await Assert.That(svc.VerifyPassword(admins[0], "admin")).IsTrue();
    }

    [Test]
    public async Task CreateAsync_rejects_duplicate_username()
    {
        var svc = new UserService(new InMemoryUserStore());
        await svc.CreateAsync("dupe", "pw");
        await Assert.That(async () => await svc.CreateAsync("dupe", "other")).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Disabled_user_cannot_verify_password()
    {
        var svc = new UserService(new InMemoryUserStore());
        var user = await svc.CreateAsync("eve", "pw");

        await Assert.That(svc.VerifyPassword(user, "pw")).IsTrue();
        await svc.SetDisabledAsync(user.Id, true);

        var disabled = await svc.GetByIdAsync(user.Id);
        await Assert.That(disabled!.Disabled).IsTrue();
        await Assert.That(svc.VerifyPassword(disabled!, "pw")).IsFalse(); // disabled => rejected even with right password
    }

    [Test]
    public async Task ResetPasswordAsync_changes_the_verifiable_password()
    {
        var svc = new UserService(new InMemoryUserStore());
        var user = await svc.CreateAsync("frank", "old");

        await svc.ResetPasswordAsync(user.Id, "new");
        var updated = await svc.GetByIdAsync(user.Id);

        await Assert.That(svc.VerifyPassword(updated!, "old")).IsFalse();
        await Assert.That(svc.VerifyPassword(updated!, "new")).IsTrue();
    }

    [Test]
    public async Task ProvisionExternal_creates_then_finds_by_external_identity()
    {
        var svc = new UserService(new InMemoryUserStore());
        var info = new ExternalUserInfo("sub-123", "grace", "grace@corp.com");

        var created = await svc.ProvisionExternalAsync("oidc", info, autoProvision: true);
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.Provider).IsEqualTo("oidc");
        await Assert.That(created.ExternalId).IsEqualTo("sub-123");
        await Assert.That(created.PasswordHash).IsEqualTo(string.Empty); // external users have no password
        await Assert.That(svc.VerifyPassword(created, "")).IsFalse();

        // Second sign-in resolves the same user by (provider, external id).
        var again = await svc.ProvisionExternalAsync("oidc", info, autoProvision: true);
        await Assert.That(again!.Id).IsEqualTo(created.Id);
        await Assert.That((await svc.GetAllAsync()).Count).IsEqualTo(1);
    }

    [Test]
    public async Task ProvisionExternal_links_existing_account_by_email()
    {
        var store = new InMemoryUserStore();
        var svc = new UserService(store);
        var local = await svc.CreateAsync("heidi", "pw", "heidi@corp.com");

        var linked = await svc.ProvisionExternalAsync("oidc", new ExternalUserInfo("sub-9", "heidi", "heidi@corp.com"), autoProvision: true);

        await Assert.That(linked!.Id).IsEqualTo(local.Id);     // linked, not duplicated
        await Assert.That(linked.Provider).IsEqualTo("oidc");
        await Assert.That(linked.ExternalId).IsEqualTo("sub-9");
        await Assert.That((await svc.GetAllAsync()).Count).IsEqualTo(1);
    }

    [Test]
    public async Task ProvisionExternal_returns_null_when_unknown_and_auto_provision_off()
    {
        var svc = new UserService(new InMemoryUserStore());
        var result = await svc.ProvisionExternalAsync("oidc", new ExternalUserInfo("sub-x", "ivan", "ivan@corp.com"), autoProvision: false);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ProvisionExternal_rejects_disabled_linked_account()
    {
        var svc = new UserService(new InMemoryUserStore());
        var user = await svc.ProvisionExternalAsync("oidc", new ExternalUserInfo("sub-d", "judy", "judy@corp.com"), autoProvision: true);
        await svc.SetDisabledAsync(user!.Id, true);

        var result = await svc.ProvisionExternalAsync("oidc", new ExternalUserInfo("sub-d", "judy", "judy@corp.com"), autoProvision: true);
        await Assert.That(result).IsNull(); // disabled external user cannot sign in
    }
}
