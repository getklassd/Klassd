using Klassd.Core.Models;
using Klassd.Core.PropertyTypes;
using Klassd.Core.PropertyTypes.Defaults;
using Klassd.Core.Services;

namespace Klassd.UnitTests;

public class ContentTypeRegistryTests
{
    private static PageTypeRegistry PageRegistry() =>
        new([typeof(ContentTypeRegistryTests).Assembly], new PropertyTypeRegistry(DefaultPropertyTypes.All));

    private static PageTypeInfo Home() =>
        PageRegistry().GetAll().First(p => p.TypeName == nameof(TestHomePage));

    [Test]
    public async Task GetAll_contains_the_fixture_page_types()
    {
        var names = PageRegistry().GetAll().Select(p => p.TypeName).ToList();
        await Assert.That(names).Contains(nameof(TestHomePage));
        await Assert.That(names).Contains(nameof(TestChildPage));
        await Assert.That(names).Contains(nameof(TestLeafPage));
    }

    [Test]
    public async Task Exists_reflects_scanned_types()
    {
        await Assert.That(PageRegistry().Exists(nameof(TestHomePage))).IsTrue();
        await Assert.That(PageRegistry().Exists("NotAPage")).IsFalse();
    }

    [Test]
    public async Task Field_names_are_camelCased()
    {
        var names = Home().Fields.Select(f => f.Name).ToList();
        await Assert.That(names).Contains("title");
        await Assert.That(names).Contains("body");
        await Assert.That(names).Contains("blocks");
    }

    [Test]
    public async Task DisplayName_splits_PascalCase()
    {
        var sub = PageRegistry().GetAll()
            .First(p => p.TypeName == nameof(TestPartlyLocalizedPage))
            .Fields.First(f => f.Name == "subTitle");
        await Assert.That(sub.DisplayName).IsEqualTo("Sub Title");
    }

    [Test]
    public async Task FieldType_resolution_uses_clr_type_and_explicit_alias()
    {
        var fields = Home().Fields;
        await Assert.That(fields.First(f => f.Name == "title").FieldType).IsEqualTo("text");
        await Assert.That(fields.First(f => f.Name == "body").FieldType).IsEqualTo("textarea");
        await Assert.That(fields.First(f => f.Name == "blocks").FieldType).IsEqualTo("blocks");
    }

    [Test]
    public async Task AllowedChildren_null_when_attribute_absent()
    {
        var child = PageRegistry().GetAll().First(p => p.TypeName == nameof(TestChildPage));
        await Assert.That(child.AllowedChildren).IsNull();
    }

    [Test]
    public async Task AllowedChildren_empty_when_attribute_has_no_types()
    {
        var leaf = PageRegistry().GetAll().First(p => p.TypeName == nameof(TestLeafPage));
        await Assert.That(leaf.AllowedChildren).IsNotNull();
        await Assert.That(leaf.AllowedChildren!).IsEmpty();
    }

    [Test]
    public async Task AllowedChildren_lists_specific_types()
    {
        await Assert.That(Home().AllowedChildren!).Contains(nameof(TestChildPage));
        await Assert.That(Home().AllowedChildren!).Count().IsEqualTo(1);
    }

    private static PageTypeInfo RelationPage() =>
        PageRegistry().GetAll().First(p => p.TypeName == nameof(TestRelationPage));

    [Test]
    public async Task PageReference_field_resolves_to_relationship()
    {
        await Assert.That(RelationPage().Fields.First(f => f.Name == "related").FieldType)
            .IsEqualTo("relationship");
    }

    [Test]
    public async Task MediaReference_field_resolves_to_media()
    {
        await Assert.That(RelationPage().Fields.First(f => f.Name == "picture").FieldType)
            .IsEqualTo("media");
    }

    [Test]
    public async Task AllowedRelationTypes_lists_specified_page_types()
    {
        var related = RelationPage().Fields.First(f => f.Name == "related");
        await Assert.That(related.AllowedRelationTypes!).Contains(nameof(TestChildPage));
        await Assert.That(related.AllowedRelationTypes!).Count().IsEqualTo(1);
    }

    [Test]
    public async Task AllowedRelationTypes_null_when_attribute_absent()
    {
        var any = RelationPage().Fields.First(f => f.Name == "anyRelated");
        await Assert.That(any.AllowedRelationTypes).IsNull();
    }

    [Test]
    public async Task AllowedRelationTypes_null_for_non_relationship_fields()
    {
        await Assert.That(Home().Fields.First(f => f.Name == "title").AllowedRelationTypes).IsNull();
    }

    [Test]
    public async Task IsLocalized_true_for_LocalizedPage_attribute()
    {
        await Assert.That(Home().IsLocalized).IsTrue();
    }

    [Test]
    public async Task IsLocalized_true_when_any_property_is_localized()
    {
        var partly = PageRegistry().GetAll().First(p => p.TypeName == nameof(TestPartlyLocalizedPage));
        await Assert.That(partly.IsLocalized).IsTrue();
    }

    [Test]
    public async Task IsLocalized_false_when_no_localization()
    {
        var child = PageRegistry().GetAll().First(p => p.TypeName == nameof(TestChildPage));
        await Assert.That(child.IsLocalized).IsFalse();
    }

    [Test]
    public async Task Per_property_IsLocalized_respects_LocalizedPage_and_Localized()
    {
        // [LocalizedPage] → all properties localized.
        await Assert.That(Home().Fields.All(f => f.IsLocalized)).IsTrue();

        var partly = PageRegistry().GetAll().First(p => p.TypeName == nameof(TestPartlyLocalizedPage));
        await Assert.That(partly.Fields.First(f => f.Name == "subTitle").IsLocalized).IsTrue();
        await Assert.That(partly.Fields.First(f => f.Name == "plainText").IsLocalized).IsFalse();
    }

    [Test]
    public async Task DefaultSlug_is_empty_string_for_root_page()
    {
        await Assert.That(Home().DefaultSlug).IsEqualTo("");
    }

    [Test]
    public async Task DefaultSlug_is_null_when_attribute_absent()
    {
        var child = PageRegistry().GetAll().First(p => p.TypeName == nameof(TestChildPage));
        await Assert.That(child.DefaultSlug).IsNull();
    }

    [Test]
    public async Task BlockTypeRegistry_discovers_blocks_with_fields()
    {
        var registry = new BlockTypeRegistry(
            [typeof(ContentTypeRegistryTests).Assembly], new PropertyTypeRegistry(DefaultPropertyTypes.All));
        var block = registry.GetAll().FirstOrDefault(b => b.TypeName == nameof(TestBlock));

        await Assert.That(block).IsNotNull();
        await Assert.That(block!.Fields.First(f => f.Name == "heading").FieldType).IsEqualTo("text");
        await Assert.That(block.Fields.First(f => f.Name == "body").FieldType).IsEqualTo("textarea");
    }
}
