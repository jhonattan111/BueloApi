using ClosedXML.Excel;
using Buelo.Engine.Ir;

namespace Buelo.Engine.Renderers;

/// <summary>
/// The ClosedXML recipe for the <see cref="BueloDocument"/> IR: the same IR that feeds the PDF
/// recipe also produces a spreadsheet. Tables map to worksheet rows (header + data + footer);
/// text/markdown become rows. Page-only constructs (page numbers, page breaks) degrade gracefully.
/// </summary>
public sealed class BueloDocumentExcelRenderer
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public static string ContentType => XlsxContentType;

    public byte[] Render(BueloDocument document)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Report");

        var row = 1;
        row = WriteNodes(sheet, document.Page.Header, row);
        row = WriteNodes(sheet, document.Page.Content, row);
        WriteNodes(sheet, document.Page.Footer, row);

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static int WriteNodes(IXLWorksheet sheet, IReadOnlyList<Node> nodes, int row)
    {
        foreach (var node in nodes)
            row = WriteNode(sheet, node, row);
        return row;
    }

    private static int WriteNode(IXLWorksheet sheet, Node node, int row)
    {
        switch (node)
        {
            case TextNode text:
                ApplyText(sheet.Cell(row, 1), ConcatRuns(text.Runs), text.Style);
                return row + 1;

            case TableNode table:
                return WriteTable(sheet, table, row);

            case ColumnNode column:
                return WriteNodes(sheet, column.Children, row);

            case ContainerNode container:
                return WriteNodes(sheet, container.Children, row);

            case RowNode rowNode:
                var col = 1;
                foreach (var item in rowNode.Items)
                    sheet.Cell(row, col++).Value = FlattenText(item.Child);
                return row + 1;

            case ImageNode:
                sheet.Cell(row, 1).Value = "[imagem]";
                return row + 1;

            case SpacerNode or DividerNode:
                return row + 1; // blank line

            case PageBreakNode:
                return row; // no equivalent in a flat sheet

            default:
                return row;
        }
    }

    private static int WriteTable(IXLWorksheet sheet, TableNode table, int row)
    {
        var columns = table.Columns.Count;

        if (table.Columns.Any(c => !string.IsNullOrEmpty(c.Header)))
        {
            for (var i = 0; i < columns; i++)
            {
                var cell = sheet.Cell(row, i + 1);
                cell.Value = table.Columns[i].Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEEEEE");
            }
            row++;
        }

        foreach (var section in table.Sections)
        {
            if (section.Header is { } groupHeader)
                row = WriteSpanningRow(sheet, groupHeader.Text, columns, row, bold: true);

            foreach (var tableRow in section.Rows)
            {
                for (var i = 0; i < tableRow.Cells.Count && i < columns; i++)
                    ApplyText(sheet.Cell(row, i + 1), tableRow.Cells[i].Text, tableRow.Cells[i].Style);
                row++;
            }

            if (section.Footer is { } groupFooter)
                row = WriteSpanningRow(sheet, groupFooter.Text, columns, row, bold: true);
        }

        if (table.Footer.Count > 0)
        {
            var col = 1;
            foreach (var footerCell in table.Footer)
            {
                var span = Math.Max(1, footerCell.Span);
                ApplyText(sheet.Cell(row, col), footerCell.Text, footerCell.Style);
                if (span > 1)
                    sheet.Range(row, col, row, col + span - 1).Merge();
                col += span;
            }
            row++;
        }

        return row + 1; // blank line after the table
    }

    private static int WriteSpanningRow(IXLWorksheet sheet, string text, int columns, int row, bool bold)
    {
        var cell = sheet.Cell(row, 1);
        cell.Value = text;
        cell.Style.Font.Bold = bold;
        if (columns > 1)
            sheet.Range(row, 1, row, columns).Merge();
        return row + 1;
    }

    private static void ApplyText(IXLCell cell, string text, Style style)
    {
        cell.Value = text;
        if (style.Bold == true) cell.Style.Font.Bold = true;
        if (style.Italic == true) cell.Style.Font.Italic = true;
        if (style.Size is { } size) cell.Style.Font.FontSize = size;

        var color = TryColor(style.Color);
        if (color is not null) cell.Style.Font.FontColor = color;

        cell.Style.Alignment.Horizontal = style.Align switch
        {
            TextAlign.Center => XLAlignmentHorizontalValues.Center,
            TextAlign.Right => XLAlignmentHorizontalValues.Right,
            _ => XLAlignmentHorizontalValues.Left,
        };
    }

    private static XLColor? TryColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;
        try
        {
            return XLColor.FromHtml(hex);
        }
        catch
        {
            return null; // 3-digit or malformed hex — skip rather than fail the whole render
        }
    }

    private static string ConcatRuns(IReadOnlyList<Run> runs) => string.Concat(runs.Select(r => r.Text));

    private static string FlattenText(Node node) => node switch
    {
        TextNode text => ConcatRuns(text.Runs),
        ColumnNode column => string.Join(" ", column.Children.Select(FlattenText)),
        ContainerNode container => string.Join(" ", container.Children.Select(FlattenText)),
        RowNode row => string.Join(" ", row.Items.Select(i => FlattenText(i.Child))),
        _ => string.Empty,
    };
}
