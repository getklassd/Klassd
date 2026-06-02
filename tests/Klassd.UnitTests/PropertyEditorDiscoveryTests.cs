using Klassd.Core.PropertyTypes;

namespace Klassd.UnitTests;

public class PropertyEditorDiscoveryTests
{
    private static IReadOnlyList<IPropertyType> Discovered() =>
        PropertyEditorDiscovery.Discover([typeof(PropertyEditorDiscoveryTests).Assembly]).ToList();

    [Test]
    public async Task Discover_finds_color_editor()
    {
        var color = Discovered().FirstOrDefault(p => p.Alias == "color");
        await Assert.That(color).IsNotNull();
        await Assert.That(color!.EditorComponent).IsEqualTo(typeof(ColorEditor));
        await Assert.That(color.ClrTypes).IsEmpty();
    }

    [Test]
    public async Task Discover_finds_rating_editor_with_clr_type()
    {
        var rating = Discovered().FirstOrDefault(p => p.Alias == "rating");
        await Assert.That(rating).IsNotNull();
        await Assert.That(rating!.EditorComponent).IsEqualTo(typeof(RatingEditor));
        await Assert.That(rating.ClrTypes).Contains(typeof(int));
    }

    [Test]
    public async Task Discovered_types_register_and_resolve()
    {
        var registry = new PropertyTypeRegistry(Discovered());
        // rating declared typeof(int) as its CLR mapping.
        await Assert.That(registry.ResolveAlias(typeof(int), null)).IsEqualTo("rating");
        await Assert.That(registry.GetEditorComponent("color")).IsEqualTo(typeof(ColorEditor));
    }
}
