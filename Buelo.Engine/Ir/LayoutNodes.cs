namespace Buelo.Engine.Ir;

/// <summary>Items laid out side by side (blueprint §4 <c>row</c>).</summary>
public sealed class RowNode : Node
{
    public IReadOnlyList<RowItem> Items { get; init; } = [];
    public float? Spacing { get; init; }
}

public sealed class RowItem
{
    public ColumnWidth Width { get; init; } = ColumnWidth.Relative(1);
    public Node Child { get; init; } = new TextNode();
}

/// <summary>Items stacked vertically (blueprint §4 <c>column</c>).</summary>
public sealed class ColumnNode : Node
{
    public IReadOnlyList<Node> Children { get; init; } = [];
    public float? Spacing { get; init; }
}

/// <summary>A bordered/filled/padded container (blueprint §4 <c>card</c>/<c>panel</c>). Box look comes from <see cref="Node.Style"/>.</summary>
public sealed class ContainerNode : Node
{
    public IReadOnlyList<Node> Children { get; init; } = [];
}

/// <summary>An image from a URL, base64 data URI or workspace artefact (blueprint §4 <c>image</c>).</summary>
public sealed class ImageNode : Node
{
    public byte[]? Data { get; init; }
    public string? Url { get; init; }
    public ImageFit Fit { get; init; } = ImageFit.Width;
    public float? Width { get; init; }
    public float? Height { get; init; }
}

public enum ImageFit { Width, Height, Area, Unproportional }

/// <summary>Vertical space.</summary>
public sealed class SpacerNode : Node
{
    public float Height { get; init; } = 10;
}

/// <summary>A horizontal rule. Thickness/color come from <see cref="Node.Style"/>.</summary>
public sealed class DividerNode : Node;

/// <summary>Forces a page break.</summary>
public sealed class PageBreakNode : Node;
