using Klassd.Backoffice;

namespace Klassd.UnitTests;

/// <summary>
/// Local-login policy now lives on <see cref="CmsOptions"/> as a straight passthrough to
/// Klassd.Auth's cookie options; the lockout-safety (keep the local form when no SSO provider
/// is configured) is enforced by the login page against Klassd.Auth's ExternalLoginRegistry.
/// </summary>
public class LocalLoginPolicyTests
{
    [Test]
    public async Task Local_login_enabled_by_default()
    {
        await Assert.That(new CmsOptions().LocalLoginEnabled).IsTrue();
    }

    [Test]
    public async Task Disabling_local_login_is_reflected()
    {
        var opts = new CmsOptions { AllowLocalLogin = false };
        await Assert.That(opts.LocalLoginEnabled).IsFalse();
    }

    [Test]
    public async Task Auto_provision_external_users_on_by_default()
    {
        await Assert.That(new CmsOptions().AutoProvisionExternalUsers).IsTrue();
    }
}
