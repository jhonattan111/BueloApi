using System.Globalization;
using System.Text;
using Buelo.Engine.Ir;

namespace Buelo.Engine.Declarative;

/// <summary>
/// "Eject": generates an equivalent C# <c>IDocument</c> (QuestPDF) from a resolved
/// <see cref="BueloDocument"/> (blueprint §10) — the graduation path from declarative to code. The
/// IR is already fully evaluated, so the emitted class is self-contained and takes no data. Core
/// nodes are covered; images degrade to a placeholder (their bytes aren't embedded in source).
/// </summary>
public static class CSharpEjector
{
    public static string Eject(BueloDocument document, string className = "EjectedDocument")
    {
        var sb = new StringBuilder();
        sb.AppendLine("using QuestPDF.Fluent;");
        sb.AppendLine("using QuestPDF.Helpers;");
        sb.AppendLine("using QuestPDF.Infrastructure;");
        sb.AppendLine();
        sb.AppendLine($"public class {className} : IDocument");
        sb.AppendLine("{");
        sb.AppendLine("    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;");
        sb.AppendLine();
        sb.AppendLine("    public void Compose(IDocumentContainer container)");
        sb.AppendLine("    {");
        sb.AppendLine("        container.Page(page =>");
        sb.AppendLine("        {");

        var settings = document.Meta.PageSettings;
        var landscape = string.Equals(document.Meta.Orientation, "landscape", StringComparison.OrdinalIgnoreCase);
        sb.AppendLine($"            page.Size(PageSizes.{ResolvePageSize(settings.PageSize)}{(landscape ? ".Landscape()" : "")});");
        sb.AppendLine($"            page.MarginHorizontal({F(settings.MarginHorizontal)}, Unit.Centimetre);");
        sb.AppendLine($"            page.MarginVertical({F(settings.MarginVertical)}, Unit.Centimetre);");
        sb.AppendLine($"            page.PageColor(\"{settings.BackgroundColor}\");");
        sb.AppendLine($"            page.DefaultTextStyle(t => t.FontSize({settings.DefaultFontSize}).FontColor(\"{settings.DefaultTextColor}\"));");

        EmitBand(sb, "page.Header()", document.Page.Header, 3);
        EmitBand(sb, "page.Content()", document.Page.Content, 3);
        EmitBand(sb, "page.Footer()", document.Page.Footer, 3);

        sb.AppendLine("        });");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitBand(StringBuilder sb, string accessor, IReadOnlyList<Node> nodes, int indent)
    {
        if (nodes.Count == 0)
            return;

        var pad = new string(' ', indent * 4);
        sb.AppendLine($"{pad}{accessor}.Column(col =>");
        sb.AppendLine($"{pad}{{");
        foreach (var node in nodes)
            EmitNode(sb, $"col.Item()", node, indent + 1);
        sb.AppendLine($"{pad}}});");
    }

    private static void EmitNode(StringBuilder sb, string container, Node node, int indent)
    {
        var pad = new string(' ', indent * 4);
        switch (node)
        {
            case TextNode text:
                sb.AppendLine($"{pad}{container}.Text(t =>");
                sb.AppendLine($"{pad}{{");
                EmitAlign(sb, "t", text.Style.Align, indent + 1);
                foreach (var run in text.Runs)
                    EmitRun(sb, run, indent + 1);
                sb.AppendLine($"{pad}}});");
                break;

            case ColumnNode column:
                sb.AppendLine($"{pad}{container}.Column(c =>");
                sb.AppendLine($"{pad}{{");
                foreach (var child in column.Children)
                    EmitNode(sb, "c.Item()", child, indent + 1);
                sb.AppendLine($"{pad}}});");
                break;

            case RowNode row:
                sb.AppendLine($"{pad}{container}.Row(r =>");
                sb.AppendLine($"{pad}{{");
                foreach (var item in row.Items)
                {
                    var itemExpr = item.Width.Kind == ColumnWidthKind.Constant
                        ? $"r.ConstantItem({F(item.Width.Value)})"
                        : $"r.RelativeItem({F(item.Width.Value)})";
                    EmitNode(sb, itemExpr, item.Child, indent + 1);
                }
                sb.AppendLine($"{pad}}});");
                break;

            case ContainerNode box:
                var boxExpr = container;
                if (!string.IsNullOrWhiteSpace(box.Style.Background)) boxExpr += $".Background(\"{box.Style.Background}\")";
                if (box.Style.BorderWidth is { } bw && bw > 0)
                {
                    boxExpr += $".Border({F(bw)})";
                    if (!string.IsNullOrWhiteSpace(box.Style.BorderColor)) boxExpr += $".BorderColor(\"{box.Style.BorderColor}\")";
                }
                if (box.Style.Padding is { } p) boxExpr += $".Padding({F(p)})";
                sb.AppendLine($"{pad}{boxExpr}.Column(c =>");
                sb.AppendLine($"{pad}{{");
                foreach (var child in box.Children)
                    EmitNode(sb, "c.Item()", child, indent + 1);
                sb.AppendLine($"{pad}}});");
                break;

            case SpacerNode spacer:
                sb.AppendLine($"{pad}{container}.Height({F(spacer.Height)});");
                break;

            case DividerNode divider:
                var line = $"{container}.LineHorizontal({F(divider.Style.BorderWidth ?? 1f)})";
                if (!string.IsNullOrWhiteSpace(divider.Style.BorderColor)) line += $".LineColor(\"{divider.Style.BorderColor}\")";
                sb.AppendLine($"{pad}{line};");
                break;

            case PageBreakNode:
                sb.AppendLine($"{pad}{container}.PageBreak();");
                break;

            case ImageNode:
                sb.AppendLine($"{pad}{container}.Text(\"[imagem]\");");
                break;

            case TableNode table:
                EmitTable(sb, container, table, indent);
                break;
        }
    }

    private static void EmitTable(StringBuilder sb, string container, TableNode table, int indent)
    {
        var pad = new string(' ', indent * 4);
        sb.AppendLine($"{pad}{container}.Table(table =>");
        sb.AppendLine($"{pad}{{");
        var inner = new string(' ', (indent + 1) * 4);

        sb.AppendLine($"{inner}table.ColumnsDefinition(cols =>");
        sb.AppendLine($"{inner}{{");
        foreach (var col in table.Columns)
            sb.AppendLine(col.Width.Kind == ColumnWidthKind.Constant
                ? $"{inner}    cols.ConstantColumn({F(col.Width.Value)});"
                : $"{inner}    cols.RelativeColumn({F(col.Width.Value)});");
        sb.AppendLine($"{inner}}});");

        if (table.Columns.Any(c => !string.IsNullOrEmpty(c.Header)))
        {
            sb.AppendLine($"{inner}table.Header(h =>");
            sb.AppendLine($"{inner}{{");
            foreach (var col in table.Columns)
                sb.AppendLine($"{inner}    h.Cell().Text(\"{Escape(col.Header)}\").Bold();");
            sb.AppendLine($"{inner}}});");
        }

        foreach (var section in table.Sections)
        {
            if (section.Header is { } gh)
                EmitCell(sb, "table", gh, inner);
            foreach (var rowCells in section.Rows)
                foreach (var cell in rowCells.Cells)
                    EmitCell(sb, "table", cell, inner);
            if (section.Footer is { } gf)
                EmitCell(sb, "table", gf, inner);
        }

        if (table.Footer.Count > 0)
        {
            sb.AppendLine($"{inner}table.Footer(f =>");
            sb.AppendLine($"{inner}{{");
            foreach (var cell in table.Footer)
                EmitCell(sb, "f", cell, inner + "    ");
            sb.AppendLine($"{inner}}});");
        }

        sb.AppendLine($"{pad}}});");
    }

    private static void EmitCell(StringBuilder sb, string owner, TableCell cell, string pad)
    {
        var expr = $"{owner}.Cell().ColumnSpan({(uint)Math.Max(1, cell.Span)})";
        var text = $".Text(\"{Escape(cell.Text)}\")";
        if (cell.Style.Bold == true) text += ".Bold()";
        sb.AppendLine($"{pad}{expr}{text};");
    }

    private static void EmitRun(StringBuilder sb, Run run, int indent)
    {
        var pad = new string(' ', indent * 4);
        var span = run.Dynamic switch
        {
            RunDynamic.PageNumber => "t.CurrentPageNumber()",
            RunDynamic.TotalPages => "t.TotalPages()",
            _ => $"t.Span(\"{Escape(run.Text)}\")",
        };

        if (run.Style.Bold == true) span += ".Bold()";
        if (run.Style.Italic == true) span += ".Italic()";
        if (run.Style.Size is { } size) span += $".FontSize({F(size)})";
        if (!string.IsNullOrWhiteSpace(run.Style.Color)) span += $".FontColor(\"{run.Style.Color}\")";
        sb.AppendLine($"{pad}{span};");
    }

    private static void EmitAlign(StringBuilder sb, string text, TextAlign? align, int indent)
    {
        var call = align switch
        {
            TextAlign.Center => $"{text}.AlignCenter();",
            TextAlign.Right => $"{text}.AlignRight();",
            TextAlign.Justify => $"{text}.Justify();",
            _ => null,
        };
        if (call is not null)
            sb.AppendLine($"{new string(' ', indent * 4)}{call}");
    }

    private static string ResolvePageSize(string size) => size?.Trim().ToUpperInvariant() switch
    {
        "A3" => "A3",
        "A5" => "A5",
        "LETTER" => "Letter",
        "LEGAL" => "Legal",
        _ => "A4",
    };

    private static string F(float value) => value.ToString(CultureInfo.InvariantCulture) + "f";

    private static string Escape(string? value)
        => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
}
