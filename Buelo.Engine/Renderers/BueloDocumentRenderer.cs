using Buelo.Contracts;
using Buelo.Engine.Ir;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Buelo.Engine.Renderers;

/// <summary>
/// The QuestPDF recipe for the <see cref="BueloDocument"/> IR: walks the resolved tree and
/// composes a QuestPDF <see cref="IDocument"/>. This is the declarative counterpart to the
/// Roslyn path in <see cref="TemplateEngine"/> — the declarative engine produces an IR, this
/// turns it into PDF bytes. (Raw C# templates bypass the IR and go straight to QuestPDF.)
/// </summary>
public sealed class BueloDocumentRenderer(BueloDocument document) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public byte[] RenderPdf() => this.GeneratePdf();

    public void Compose(IDocumentContainer container)
    {
        var settings = document.Meta.PageSettings;

        container.Page(page =>
        {
            page.Size(ResolvePageSize(settings.PageSize, document.Meta.Orientation));
            page.MarginHorizontal(settings.MarginHorizontal, Unit.Centimetre);
            page.MarginVertical(settings.MarginVertical, Unit.Centimetre);
            page.PageColor(settings.BackgroundColor);
            page.DefaultTextStyle(t => t.FontSize(settings.DefaultFontSize).FontColor(settings.DefaultTextColor));

            if (document.Page.Header.Count > 0)
                page.Header().Element(c => ComposeStack(c, document.Page.Header));

            page.Content().Element(c => ComposeStack(c, document.Page.Content));

            if (document.Page.Footer.Count > 0)
                page.Footer().Element(c => ComposeStack(c, document.Page.Footer));
        });
    }

    private static void ComposeStack(IContainer container, IReadOnlyList<Node> nodes)
    {
        container.Column(column =>
        {
            foreach (var node in nodes)
                ComposeNode(column.Item(), node);
        });
    }

    private static void ComposeNode(IContainer container, Node node)
    {
        switch (node)
        {
            case TextNode text: ComposeText(container, text); break;
            case TableNode table: ComposeTable(container, table); break;
            case RowNode row: ComposeRow(container, row); break;
            case ColumnNode column: ComposeColumn(container, column); break;
            case ContainerNode box: ComposeContainer(container, box); break;
            case ImageNode image: ComposeImage(container, image); break;
            case SpacerNode spacer: container.Height(spacer.Height); break;
            case DividerNode divider: ComposeDivider(container, divider); break;
            case PageBreakNode: container.PageBreak(); break;
        }
    }

    private static void ComposeText(IContainer container, TextNode node)
    {
        container.Text(text =>
        {
            ApplyAlignment(text, node.Style.Align);
            foreach (var run in node.Runs)
            {
                var span = run.Dynamic switch
                {
                    RunDynamic.PageNumber => text.CurrentPageNumber(),
                    RunDynamic.TotalPages => text.TotalPages(),
                    _ => text.Span(run.Text),
                };
                ApplyRunStyle(span, run.Style);
            }
        });
    }

    // ── Layout primitives (blueprint §4) ───────────────────────────────────────

    private static void ComposeRow(IContainer container, RowNode node)
    {
        container.Row(row =>
        {
            if (node.Spacing is { } spacing)
                row.Spacing(spacing);

            foreach (var item in node.Items)
            {
                var cell = item.Width.Kind == ColumnWidthKind.Constant
                    ? row.ConstantItem(item.Width.Value)
                    : row.RelativeItem(item.Width.Value);
                ComposeNode(cell, item.Child);
            }
        });
    }

    private static void ComposeColumn(IContainer container, ColumnNode node)
    {
        container.Column(column =>
        {
            if (node.Spacing is { } spacing)
                column.Spacing(spacing);

            foreach (var child in node.Children)
                ComposeNode(column.Item(), child);
        });
    }

    private static void ComposeContainer(IContainer container, ContainerNode node)
    {
        var box = node.Style.Background is { Length: > 0 } bg ? container.Background(bg) : container;

        if (node.Style.BorderWidth is { } width && width > 0)
        {
            box = box.Border(width);
            if (node.Style.BorderColor is { Length: > 0 } color)
                box = box.BorderColor(color);
        }

        if (node.Style.Padding is { } padding)
            box = box.Padding(padding);

        box.Column(column =>
        {
            foreach (var child in node.Children)
                ComposeNode(column.Item(), child);
        });
    }

    private static void ComposeImage(IContainer container, ImageNode node)
    {
        if (node.Width is { } w) container = container.Width(w);
        if (node.Height is { } h) container = container.Height(h);

        if (node.Data is { Length: > 0 } bytes)
        {
            ApplyImageFit(container.Image(bytes), node.Fit);
        }
        else if (node.Url is { Length: > 0 } path && File.Exists(path))
        {
            ApplyImageFit(container.Image(path), node.Fit);
        }
        else
        {
            container.Text("[imagem]").FontColor("#999999").Italic();
        }
    }

    private static void ApplyImageFit(ImageDescriptor image, ImageFit fit)
    {
        switch (fit)
        {
            case ImageFit.Height: image.FitHeight(); break;
            case ImageFit.Area: image.FitArea(); break;
            case ImageFit.Unproportional: image.FitUnproportionally(); break;
            default: image.FitWidth(); break;
        }
    }

    private static void ComposeDivider(IContainer container, DividerNode node)
    {
        var line = container.LineHorizontal(node.Style.BorderWidth ?? 1f);
        if (node.Style.BorderColor is { Length: > 0 } color)
            line.LineColor(color);
    }

    // ── Table (blueprint §5) ───────────────────────────────────────────────────

    private static readonly Style HeaderCellStyle = new()
    {
        Bold = true,
        PaddingVertical = 3,
        BorderBottomWidth = 1,
        BorderBottomColor = "#999999",
    };

    private static void ComposeTable(IContainer container, TableNode table)
    {
        container.Table(t =>
        {
            t.ColumnsDefinition(columns =>
            {
                foreach (var column in table.Columns)
                {
                    if (column.Width.Kind == ColumnWidthKind.Constant)
                        columns.ConstantColumn(column.Width.Value);
                    else
                        columns.RelativeColumn(column.Width.Value);
                }
            });

            if (table.Columns.Any(c => !string.IsNullOrEmpty(c.Header)))
            {
                t.Header(header =>
                {
                    foreach (var column in table.Columns)
                        FillCell(header.Cell(), new TableCell { Text = column.Header, Style = HeaderCellStyle }, null);
                });
            }

            foreach (var section in table.Sections)
            {
                if (section.Header is { } groupHeader)
                    FillCell(t.Cell().ColumnSpan((uint)groupHeader.Span), groupHeader, null);

                foreach (var row in section.Rows)
                    foreach (var cell in row.Cells)
                        FillCell(t.Cell().ColumnSpan((uint)Math.Max(1, cell.Span)), cell, row.RowStyle);

                if (section.Footer is { } groupFooter)
                    FillCell(t.Cell().ColumnSpan((uint)groupFooter.Span), groupFooter, null);
            }

            if (table.Footer.Count > 0)
            {
                t.Footer(footer =>
                {
                    foreach (var cell in table.Footer)
                        FillCell(footer.Cell().ColumnSpan((uint)Math.Max(1, cell.Span)), cell, null);
                });
            }
        });
    }

    private static void FillCell(IContainer cell, TableCell model, Style? rowStyle)
    {
        var box = ApplyCellBox(cell, model.Style, rowStyle);
        box.Text(text =>
        {
            ApplyAlignment(text, model.Style.Align);
            ApplyRunStyle(text.Span(model.Text), model.Style);
        });
    }

    private static IContainer ApplyCellBox(IContainer container, Style cell, Style? row)
    {
        var background = cell.Background ?? row?.Background;
        if (!string.IsNullOrWhiteSpace(background))
            container = container.Background(background);

        var borderWidth = cell.BorderBottomWidth ?? row?.BorderBottomWidth;
        if (borderWidth is { } width && width > 0)
        {
            container = container.BorderBottom(width);
            var borderColor = cell.BorderBottomColor ?? row?.BorderBottomColor;
            if (!string.IsNullOrWhiteSpace(borderColor))
                container = container.BorderColor(borderColor);
        }

        var padding = cell.PaddingVertical ?? row?.PaddingVertical;
        if (padding is { } p)
            container = container.PaddingVertical(p);

        return container;
    }

    private static void ApplyAlignment(TextDescriptor text, TextAlign? align)
    {
        switch (align)
        {
            case TextAlign.Center: text.AlignCenter(); break;
            case TextAlign.Right: text.AlignRight(); break;
            case TextAlign.Justify: text.Justify(); break;
            case TextAlign.Left: text.AlignLeft(); break;
        }
    }

    private static void ApplyRunStyle(TextSpanDescriptor span, Style style)
    {
        if (style.Bold == true) span.Bold();
        if (style.Italic == true) span.Italic();
        if (style.Size is { } size) span.FontSize(size);
        if (!string.IsNullOrWhiteSpace(style.Color)) span.FontColor(style.Color);
        if (!string.IsNullOrWhiteSpace(style.Background)) span.BackgroundColor(style.Background);
    }

    private static PageSize ResolvePageSize(string size, string? orientation)
    {
        var pageSize = size?.Trim().ToUpperInvariant() switch
        {
            "A3" => PageSizes.A3,
            "A5" => PageSizes.A5,
            "LETTER" => PageSizes.Letter,
            "LEGAL" => PageSizes.Legal,
            _ => PageSizes.A4,
        };

        return string.Equals(orientation, "landscape", StringComparison.OrdinalIgnoreCase)
            ? pageSize.Landscape()
            : pageSize;
    }
}
