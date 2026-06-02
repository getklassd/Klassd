using Klassd.Backoffice;

namespace Klassd.UnitTests;

public class DeliveryApiKeyTests
{
    [Test]
    public async Task Public_when_not_required_regardless_of_keys()
    {
        await Assert.That(DeliveryApiKey.Evaluate(require: false, configuredKey: null, providedKey: null))
            .IsEqualTo(DeliveryAccess.Public);
        await Assert.That(DeliveryApiKey.Evaluate(require: false, configuredKey: "k", providedKey: "wrong"))
            .IsEqualTo(DeliveryAccess.Public);
    }

    [Test]
    public async Task NotConfigured_when_required_but_no_key_set()
    {
        await Assert.That(DeliveryApiKey.Evaluate(require: true, configuredKey: null, providedKey: "anything"))
            .IsEqualTo(DeliveryAccess.NotConfigured);
        await Assert.That(DeliveryApiKey.Evaluate(require: true, configuredKey: "", providedKey: "anything"))
            .IsEqualTo(DeliveryAccess.NotConfigured);
    }

    [Test]
    public async Task Authorized_only_on_exact_match()
    {
        await Assert.That(DeliveryApiKey.Evaluate(true, "secret", "secret")).IsEqualTo(DeliveryAccess.Authorized);
        await Assert.That(DeliveryApiKey.Evaluate(true, "secret", "Secret")).IsEqualTo(DeliveryAccess.Unauthorized);
        await Assert.That(DeliveryApiKey.Evaluate(true, "secret", "")).IsEqualTo(DeliveryAccess.Unauthorized);
        await Assert.That(DeliveryApiKey.Evaluate(true, "secret", null)).IsEqualTo(DeliveryAccess.Unauthorized);
    }
}
