namespace Buelo.Engine.Ir;

/// <summary>
/// A fully-resolved, data-oriented table (blueprint §5). All expressions are already evaluated:
/// rows are concrete cells, groups are materialized, the footer is computed. A non-grouped table
/// has a single <see cref="TableSection"/> with no header/footer; a grouped table has one section
/// per group.
/// </summary>
public sealed class TableNode : Node
{
    public IReadOnlyList<TableColumn> Columns { get; init; } = [];
    public IReadOnlyList<TableSection> Sections { get; init; } = [];
    public IReadOnlyList<TableCell> Footer { get; init; } = [];
}

public sealed class TableColumn
{
    public ColumnWidth Width { get; init; } = ColumnWidth.Relative(1);
    public string Header { get; init; } = string.Empty;
    public Style HeaderStyle { get; init; } = new();
}

public enum ColumnWidthKind { Relative, Constant }

/// <summary>A column width: relative weight (<c>*</c>, <c>3*</c>) or a constant size in points (<c>120px</c>, <c>2cm</c>).</summary>
public sealed class ColumnWidth
{
    public ColumnWidthKind Kind { get; init; }
    public float Value { get; init; }

    public static ColumnWidth Relative(float weight) => new() { Kind = ColumnWidthKind.Relative, Value = weight };
    public static ColumnWidth Constant(float points) => new() { Kind = ColumnWidthKind.Constant, Value = points };
}

/// <summary>One band of a table: an optional group header, its rows, and an optional group footer.</summary>
public sealed class TableSection
{
    public TableCell? Header { get; init; }
    public IReadOnlyList<TableRow> Rows { get; init; } = [];
    public TableCell? Footer { get; init; }
}

public sealed class TableRow
{
    public IReadOnlyList<TableCell> Cells { get; init; } = [];
    public Style RowStyle { get; init; } = new();
}

public sealed class TableCell
{
    public string Text { get; init; } = string.Empty;
    public Style Style { get; init; } = new();
    public int Span { get; init; } = 1;
}
