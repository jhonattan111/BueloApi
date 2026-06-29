using Buelo.Contracts;

namespace Buelo.Engine.Ir;

/// <summary>
/// The intermediate representation (IR) produced by an engine and consumed by a recipe.
/// Fully resolved: no pending expressions, no imports, no style-by-name — everything is
/// already evaluated and flattened. This is the contract between engine and recipe
/// (see <c>docs/blueprint-schema-canonico.md</c> §9).
/// </summary>
public sealed class BueloDocument
{
    public DocumentMeta Meta { get; init; } = new();
    public DocumentPage Page { get; init; } = new();
}

/// <summary>Document-level settings carried into the recipe.</summary>
public sealed class DocumentMeta
{
    public PageSettings PageSettings { get; init; } = PageSettings.Default();
    public string Recipe { get; init; } = "pdf";
    public string? Orientation { get; init; }   // portrait (default) | landscape
}

/// <summary>The three page bands. Header/footer repeat; content is the main flow.</summary>
public sealed class DocumentPage
{
    public IReadOnlyList<Node> Header { get; init; } = [];
    public IReadOnlyList<Node> Content { get; init; } = [];
    public IReadOnlyList<Node> Footer { get; init; } = [];
}

/// <summary>Base for every layout node. Carries a resolved <see cref="Style"/>.</summary>
public abstract class Node
{
    public Style Style { get; init; } = new();
}

/// <summary>Text composed of one or more styled runs.</summary>
public sealed class TextNode : Node
{
    public IReadOnlyList<Run> Runs { get; init; } = [];
}

/// <summary>A contiguous span of text with its own style. Dynamic runs resolve at render time.</summary>
public sealed class Run
{
    public string Text { get; init; } = string.Empty;
    public Style Style { get; init; } = new();
    public RunDynamic Dynamic { get; init; } = RunDynamic.None;
}

/// <summary>Render-time values that can't be resolved during lowering (need page layout).</summary>
public enum RunDynamic
{
    None,
    PageNumber,
    TotalPages,
}

/// <summary>
/// Resolved style. All properties are optional; null means "inherit the default".
/// (v1 subset — padding/margin/border/width/height arrive with container nodes.)
/// </summary>
public sealed class Style
{
    public string? Color { get; init; }
    public string? Background { get; init; }
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
    public float? Size { get; init; }
    public TextAlign? Align { get; init; }

    // Box properties (null = none).
    public float? Padding { get; init; }            // uniform padding (all sides)
    public float? PaddingVertical { get; init; }
    public float? BorderWidth { get; init; }        // uniform border (all sides)
    public string? BorderColor { get; init; }
    public string? BorderBottomColor { get; init; }
    public float? BorderBottomWidth { get; init; }
}

public enum TextAlign
{
    Left,
    Center,
    Right,
    Justify,
}
