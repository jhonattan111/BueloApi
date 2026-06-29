namespace Buelo.Engine.Declarative;

/// <summary>
/// The declarative model parsed from a YAML report definition (see
/// <c>docs/blueprint-schema-canonico.md</c> §3). This is the AST the interpreter lowers
/// into a <see cref="Ir.BueloDocument"/>. It still holds raw <c>{{ }}</c> expressions.
/// v1 covers <c>kind: report</c> with text blocks; more block kinds arrive incrementally.
/// </summary>
public sealed class ReportDefinition
{
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ReportMeta Meta { get; set; } = new();

    /// <summary>Modules this report uses (advisory in v1; provided modules are all resolvable).</summary>
    public List<Dictionary<string, string>> Import { get; set; } = [];

    // Optional layout wrap: the report content fills the component's "content" slot (blueprint §3/§7).
    public string? Use { get; set; }
    public Dictionary<string, string>? With { get; set; }

    // Page bands. Header/footer repeat on every page; content is the main flow.
    public List<ContentBlock> Header { get; set; } = [];
    public List<ContentBlock> Content { get; set; } = [];
    public List<ContentBlock> Footer { get; set; } = [];
}

public sealed class ReportMeta
{
    public string Engine { get; set; } = "declarative";
    public string Recipe { get; set; } = "pdf";
    public PageDef? Page { get; set; }
}

public sealed class PageDef
{
    public string? Size { get; set; }
    public string? Margin { get; set; }
    public string? Orientation { get; set; }
}

/// <summary>
/// One item in the <c>content</c> list. Each block is a map with a single key naming the
/// block kind (<c>text:</c>, later <c>row:</c>, <c>table:</c>, …). Exactly one property is set.
/// </summary>
/// <summary>
/// One block in a band/container. Exactly one block-kind property is set. <see cref="Width"/> is
/// only meaningful when the block is an item of a <c>row</c>.
/// </summary>
public sealed class ContentBlock
{
    public string? Width { get; set; }

    public TextBlock? Text { get; set; }
    public string? Markdown { get; set; }
    public TableBlock? Table { get; set; }
    public RowBlock? Row { get; set; }
    public ColumnBlock? Column { get; set; }
    public CardBlock? Card { get; set; }
    public CardBlock? Panel { get; set; }
    public ImageBlock? Image { get; set; }
    public float? Spacer { get; set; }
    public DividerBlock? Divider { get; set; }
    public DividerBlock? Line { get; set; }
    public bool? PageBreak { get; set; }

    // Component composition (blueprint §7).
    public string? Use { get; set; }                       // invoke a component by name
    public Dictionary<string, string>? With { get; set; }  // param name -> expression (evaluated in caller scope)
    public string? Slot { get; set; }                      // inside a component body: injection point
}

public sealed class TextBlock
{
    public string Value { get; set; } = string.Empty;
    public string? Class { get; set; }
    public StyleDef? Style { get; set; }
}

public sealed class RowBlock
{
    public float? Spacing { get; set; }
    public List<ContentBlock> Items { get; set; } = [];
}

public sealed class ColumnBlock
{
    public float? Spacing { get; set; }
    public List<ContentBlock> Content { get; set; } = [];
}

/// <summary>Container with a box look (border/background/padding) wrapping child blocks.</summary>
public sealed class CardBlock
{
    public string? Class { get; set; }
    public StyleDef? Style { get; set; }
    public List<ContentBlock> Content { get; set; } = [];
}

public sealed class ImageBlock
{
    public string? Url { get; set; }
    public string? Base64 { get; set; }
    public string? Source { get; set; }   // workspace artefact path
    public string? Fit { get; set; }      // width | height | area | unproportional
    public float? Width { get; set; }
    public float? Height { get; set; }
}

public sealed class DividerBlock
{
    public string? Color { get; set; }
    public float? Thickness { get; set; }
}

/// <summary>A data-oriented table block (blueprint §5).</summary>
public sealed class TableBlock
{
    /// <summary>Expression yielding the row array, e.g. <c>data.itens</c> (no <c>{{ }}</c>).</summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>Optional field name to group rows by.</summary>
    public string? GroupBy { get; set; }

    public StyleDef? RowStyle { get; set; }
    public List<ColumnDef> Columns { get; set; } = [];
    public GroupDef? Group { get; set; }
    public List<FooterCellDef> Footer { get; set; } = [];
}

public sealed class ColumnDef
{
    /// <summary>Width: <c>*</c>, <c>3*</c>, <c>120px</c>, <c>40%</c>, <c>2cm</c>. Default <c>*</c>.</summary>
    public string? Width { get; set; }
    public string Header { get; set; } = string.Empty;
    /// <summary>Per-row cell expression, e.g. <c>"{{ item.nome }}"</c>.</summary>
    public string Cell { get; set; } = string.Empty;
    public string? Class { get; set; }
    public string? Align { get; set; }
    public StyleDef? Style { get; set; }
}

public sealed class GroupDef
{
    public GroupBandDef? Header { get; set; }
    public GroupBandDef? Footer { get; set; }
}

public sealed class GroupBandDef
{
    public string Text { get; set; } = string.Empty;
    public string? Class { get; set; }
    public StyleDef? Style { get; set; }
}

public sealed class FooterCellDef
{
    public int Span { get; set; } = 1;
    public string? Text { get; set; }
    public string? Class { get; set; }
    public string? Align { get; set; }
    public StyleDef? Style { get; set; }
}

public sealed class StyleDef
{
    public string? Color { get; set; }
    public string? Background { get; set; }
    public bool? Bold { get; set; }
    public bool? Italic { get; set; }
    public float? Size { get; set; }
    public string? Align { get; set; }

    // Box properties, e.g. padding: 8, border: "1px #CCC", paddingY: 5, borderBottom: "1px #DDD".
    public float? Padding { get; set; }
    public string? Border { get; set; }
    public float? PaddingY { get; set; }
    public string? BorderBottom { get; set; }

    /// <summary>Only meaningful inside a <c>kind: styles</c> class: inherit from another class first.</summary>
    public string? Extends { get; set; }
}
