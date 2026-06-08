using System.Security.Claims;
using Klassd.Backoffice.Modules.Auth;
using TUnit.Core;

namespace Klassd.UnitTests;

public class CmsRolesTests
{
    [Test]
    public async Task Administrator_has_all_capabilities()
    {
        await Assert.That(CmsRoles.Resolve([CmsRoles.Administrator])).IsEqualTo(Capabilities.All);
    }

    [Test]
    public async Task Author_can_edit_but_not_publish()
    {
        var caps = CmsRoles.Resolve([CmsRoles.Author]);
        await Assert.That(caps.HasFlag(Capabilities.PagesEdit)).IsTrue();
        await Assert.That(caps.HasFlag(Capabilities.PagesPublish)).IsFalse();
        await Assert.That(caps.HasFlag(Capabilities.UsersManage)).IsFalse();
    }

    [Test]
    public async Task Editor_can_publish_but_not_manage_users()
    {
        var caps = CmsRoles.Resolve([CmsRoles.Editor]);
        await Assert.That(caps.HasFlag(Capabilities.PagesPublish)).IsTrue();
        await Assert.That(caps.HasFlag(Capabilities.UsersManage)).IsFalse();
    }

    [Test]
    public async Task Multiple_roles_union_capabilities()
    {
        var caps = CmsRoles.Resolve([CmsRoles.Author, CmsRoles.Editor]);
        await Assert.That(caps.HasFlag(Capabilities.PagesPublish)).IsTrue(); // from Editor
        await Assert.That(caps.HasFlag(Capabilities.PagesEdit)).IsTrue();
    }

    [Test]
    public async Task No_roles_is_administrator_for_back_compat()
    {
        await Assert.That(CmsRoles.Resolve([])).IsEqualTo(Capabilities.All);
        await Assert.That(CmsRoles.Resolve(null)).IsEqualTo(Capabilities.All);
    }

    [Test]
    public async Task Unknown_role_grants_nothing()
    {
        await Assert.That(CmsRoles.Resolve(["Nonexistent"])).IsEqualTo(Capabilities.None);
    }

    [Test]
    public async Task HasCapability_reads_role_claims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, CmsRoles.Author)], "test"));

        await Assert.That(principal.HasCapability(Capabilities.PagesEdit)).IsTrue();
        await Assert.That(principal.HasCapability(Capabilities.PagesPublish)).IsFalse();
    }
}
