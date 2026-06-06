using Klassd.Core.Abstractions;
using Klassd.Core.PropertyTypes;
using Klassd.Core.PropertyTypes.Defaults;

namespace Klassd.UnitTests;

public class PropertyTypeRegistryTests
{
    private static PropertyTypeRegistry Defaults() => new(DefaultPropertyTypes.All);

    [Test]
    public async Task ResolveAlias_string_maps_to_text()
    {
        await Assert.That(Defaults().ResolveAlias(typeof(string), null)).IsEqualTo("text");
    }

    [Test]
    [Arguments(typeof(int))]
    [Arguments(typeof(long))]
    public async Task ResolveAlias_int_and_long_map_to_number(Type clr)
    {
        await Assert.That(Defaults().ResolveAlias(clr, null)).IsEqualTo("number");
    }

    [Test]
    public async Task ResolveAlias_bool_maps_to_checkbox()
    {
        await Assert.That(Defaults().ResolveAlias(typeof(bool), null)).IsEqualTo("checkbox");
    }

    [Test]
    public async Task ResolveAlias_DateTime_maps_to_datetime_local()
    {
        await Assert.That(Defaults().ResolveAlias(typeof(DateTime), null)).IsEqualTo("datetime-local");
    }

    [Test]
    public async Task ResolveAlias_BlockArea_maps_to_blocks()
    {
        await Assert.That(Defaults().ResolveAlias(typeof(BlockArea), null)).IsEqualTo("blocks");
    }

    [Test]
    public async Task ResolveAlias_PageReference_maps_to_relationship()
    {
        await Assert.That(Defaults().ResolveAlias(typeof(PageReference), null)).IsEqualTo("relationship");
    }

    [Test]
    public async Task ResolveAlias_MediaReference_maps_to_media()
    {
        await Assert.That(Defaults().ResolveAlias(typeof(MediaReference), null)).IsEqualTo("media");
    }

    [Test]
    public async Task ResolveAlias_unknown_clr_type_falls_back_to_text()
    {
        await Assert.That(Defaults().ResolveAlias(typeof(Guid), null)).IsEqualTo(PropertyTypeRegistry.FallbackAlias);
    }

    [Test]
    public async Task ResolveAlias_explicit_registered_alias_wins_over_clr_mapping()
    {
        // string would map to "text", but an explicit registered alias overrides.
        await Assert.That(Defaults().ResolveAlias(typeof(string), "textarea")).IsEqualTo("textarea");
    }

    [Test]
    public async Task ResolveAlias_explicit_unregistered_alias_falls_back_to_clr_mapping()
    {
        await Assert.That(Defaults().ResolveAlias(typeof(string), "doesnotexist")).IsEqualTo("text");
    }

    [Test]
    public async Task LastRegistration_wins_per_alias()
    {
        var first = new ComponentPropertyType("dup", typeof(ColorEditor), []);
        var second = new ComponentPropertyType("dup", typeof(RatingEditor), []);
        var registry = new PropertyTypeRegistry([first, second]);

        await Assert.That(registry.Get("dup")!.EditorComponent).IsEqualTo(typeof(RatingEditor));
        await Assert.That(registry.GetEditorComponent("dup")).IsEqualTo(typeof(RatingEditor));
    }

    [Test]
    public async Task GetEditorComponent_returns_custom_type()
    {
        var custom = new ComponentPropertyType("color", typeof(ColorEditor), []);
        var registry = new PropertyTypeRegistry([.. DefaultPropertyTypes.All, custom]);

        await Assert.That(registry.GetEditorComponent("color")).IsEqualTo(typeof(ColorEditor));
    }

    [Test]
    public async Task GetEditorComponent_null_for_builtin_types()
    {
        await Assert.That(Defaults().GetEditorComponent("text")).IsNull();
        await Assert.That(Defaults().GetEditorComponent("blocks")).IsNull();
    }

    [Test]
    public async Task Get_returns_null_for_unknown_alias()
    {
        await Assert.That(Defaults().Get("nope")).IsNull();
    }
}
