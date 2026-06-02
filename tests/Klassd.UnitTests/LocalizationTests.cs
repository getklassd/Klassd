using Klassd.Core.Localization;

namespace Klassd.UnitTests;

public class LocalizationTests
{
    private static LocalizationOptions BuildOptions() =>
        new LocalizationOptions()
            .AddLocale("en", l => l.AsMandatory().AsDefault())
            .AddLocale("da", l => l.WithFallback("en"));

    [Test]
    public async Task AddLocale_records_codes_and_flags()
    {
        var opts = BuildOptions();
        await Assert.That(opts.Locales).Count().IsEqualTo(2);

        var en = opts.Locales.First(l => l.Code == "en");
        await Assert.That(en.Mandatory).IsTrue();
        await Assert.That(en.IsDefault).IsTrue();
        await Assert.That(en.FallbackTo).IsNull();

        var da = opts.Locales.First(l => l.Code == "da");
        await Assert.That(da.FallbackTo).IsEqualTo("en");
        await Assert.That(da.Mandatory).IsFalse();
        await Assert.That(da.IsDefault).IsFalse();
    }

    [Test]
    public async Task AddLocale_same_code_overrides_existing()
    {
        var opts = new LocalizationOptions()
            .AddLocale("en", l => l.AsMandatory())
            .AddLocale("en", l => l.AsDefault());

        await Assert.That(opts.Locales).Count().IsEqualTo(1);
        var en = opts.Locales.Single();
        await Assert.That(en.IsDefault).IsTrue();
        await Assert.That(en.Mandatory).IsFalse(); // second registration replaced the first
    }

    [Test]
    public async Task LoadFrom_merges_and_overrides_by_code()
    {
        var opts = BuildOptions().LoadFrom(
        [
            new LocaleConfig("da", FallbackTo: "en", Mandatory: true),  // override existing da
            new LocaleConfig("de"),                                     // new locale
        ]);

        await Assert.That(opts.Locales).Count().IsEqualTo(3);

        var da = opts.Locales.First(l => l.Code == "da");
        await Assert.That(da.Mandatory).IsTrue();
        await Assert.That(da.FallbackTo).IsEqualTo("en");

        await Assert.That(opts.Locales.Any(l => l.Code == "de")).IsTrue();
    }

    [Test]
    public async Task LoadFrom_null_is_a_noop()
    {
        var opts = BuildOptions().LoadFrom(null);
        await Assert.That(opts.Locales).Count().IsEqualTo(2);
    }

    [Test]
    public async Task LocaleRegistry_roundtrips_definitions()
    {
        var opts = BuildOptions();
        var registry = new LocaleRegistry(opts.Locales);

        await Assert.That(registry.All).Count().IsEqualTo(2);
        await Assert.That(registry.All.Select(l => l.Code)).Contains("en");
        await Assert.That(registry.All.Select(l => l.Code)).Contains("da");
    }

    [Test]
    public async Task LocaleRegistry_resolves_fallback_chain()
    {
        var registry = new LocaleRegistry(BuildOptions().Locales);
        await Assert.That(registry.GetFallbackChain("da")).IsEquivalentTo(new[] { "da", "en" });
    }
}
