using Klassd.Core.Abstractions;
using Klassd.Core.PropertyTypes;

namespace Klassd.UnitTests;

// ── Page / block fixture types (scanned by the registries) ──────────────────

[CmsPage(DefaultSlug = "")]
[AllowedChildren(typeof(TestChildPage))]
[LocalizedPage]
public class TestHomePage : PageBase
{
    public string Title { get; set; } = "";

    [CmsField(FieldType = "textarea")]
    public string Body { get; set; } = "";

    public BlockArea Blocks { get; set; } = new();
}

// No AllowedChildren attribute → null (all children allowed).
public class TestChildPage : PageBase
{
    public int Count { get; set; }
}

// AllowedChildren present with no types → empty (no children allowed).
[AllowedChildren]
public class TestLeafPage : PageBase
{
}

// Localized via a per-property [Localized] only (no [LocalizedPage]).
public class TestPartlyLocalizedPage : PageBase
{
    [Localized]
    public string SubTitle { get; set; } = "";

    public string PlainText { get; set; } = "";
}

public class TestBlock : BlockBase
{
    public string Heading { get; set; } = "";

    [CmsField(FieldType = "textarea")]
    public string Body { get; set; } = "";
}

// ── Property editor marker types (scanned by PropertyEditorDiscovery) ────────

[PropertyEditor("color")]
public class ColorEditor
{
}

[PropertyEditor("rating", typeof(int))]
public class RatingEditor
{
}
