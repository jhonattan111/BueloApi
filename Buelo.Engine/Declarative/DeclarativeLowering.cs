using System.Globalization;
using System.Text.RegularExpressions;
using Buelo.Contracts;
using Buelo.Engine.Declarative.Expressions;
using Buelo.Engine.Declarative.Modules;
using Buelo.Engine.Ir;

namespace Buelo.Engine.Declarative;

/// <summary>
/// Per-render worker that lowers a <see cref="ReportDefinition"/> + data + modules into a
/// <see cref="BueloDocument"/>. Holds the module registry, expression context and slot fills for one
/// render, so it is safe to create per call (the singleton interpreter just news one up).
/// </summary>
internal sealed partial class DeclarativeLowering
{
    [GeneratedRegex(@"\{\{(.+?)\}\}", RegexOptions.Singleline)]
    private static partial Regex TokenRegex();

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<Node>> NoSlots
        = new Dictionary<string, IReadOnlyList<Node>>();

    private readonly ModuleRegistry _registry;
    private readonly ExpressionContext _context;

    public DeclarativeLowering(ModuleRegistry registry)
    {
        _registry = registry;
        _context = registry.CreateExpressionContext();
    }

    public BueloDocument Lower(ReportDefinition definition, object? data)
    {
        var dataObject = data is null ? null : TemplateEngine.ConvertToDynamic(data);
        var scope = new Dictionary<string, object?>
        {
            ["data"] = dataObject,
            ["now"] = DateTime.Now,
            ["today"] = DateTime.Today,
            ["report"] = new Dictionary<string, object?> { ["name"] = definition.Name },
        };

        var content = LowerBlocks(definition.Content, scope, NoSlots);

        // Optional layout wrap: feed the report content into the component's "content" slot.
        if (!string.IsNullOrWhiteSpace(definition.Use))
        {
            var component = ResolveComponent(definition.Use!);
            var componentScope = BuildComponentScope(component, definition.With, scope);
            content = LowerBlocks(component.Body, componentScope,
                new Dictionary<string, IReadOnlyList<Node>> { ["content"] = content });
        }

        return new BueloDocument
        {
            Meta = new DocumentMeta
            {
                Recipe = string.IsNullOrWhiteSpace(definition.Meta.Recipe) ? "pdf" : definition.Meta.Recipe,
                PageSettings = MapPage(definition.Meta.Page),
                Orientation = definition.Meta.Page?.Orientation ?? _registry.ThemePage?.Orientation,
            },
            Page = new DocumentPage
            {
                Header = LowerBlocks(definition.Header, scope, NoSlots),
                Content = content,
                Footer = LowerBlocks(definition.Footer, scope, NoSlots),
            },
        };
    }

    // ── Blocks ─────────────────────────────────────────────────────────────────

    private List<Node> LowerBlocks(
        IEnumerable<ContentBlock> blocks, IDictionary<string, object?> scope,
        IReadOnlyDictionary<string, IReadOnlyList<Node>> slots)
    {
        var nodes = new List<Node>();
        foreach (var block in blocks)
        {
            var node = LowerBlock(block, scope, slots);
            if (node is not null)
                nodes.Add(node);
        }
        return nodes;
    }

    private Node? LowerBlock(
        ContentBlock block, IDictionary<string, object?> scope,
        IReadOnlyDictionary<string, IReadOnlyList<Node>> slots) => block switch
        {
            { Slot: { } slot } => slots.TryGetValue(slot, out var fill) ? new ColumnNode { Children = fill } : null,
            { Use: { } use } => LowerComponentUse(use, block.With, scope),
            { Text: { } text } => LowerText(text, scope),
            { Markdown: { } markdown } => LowerMarkdown(markdown, scope),
            { Table: { } table } => LowerTable(table, scope),
            { Row: { } row } => LowerRow(row, scope, slots),
            { Column: { } column } => LowerColumn(column, scope, slots),
            { Card: { } card } => LowerCard(card, scope, slots),
            { Panel: { } panel } => LowerCard(panel, scope, slots),
            { Image: { } image } => LowerImage(image),
            { Spacer: { } height } => new SpacerNode { Height = height },
            { Divider: { } divider } => LowerDivider(divider),
            { Line: { } line } => LowerDivider(line),
            { PageBreak: true } => new PageBreakNode(),
            _ => null,
        };

    private Node LowerText(TextBlock text, IDictionary<string, object?> scope)
    {
        var style = MapStyle(text.Style, text.Class);
        return new TextNode { Style = style, Runs = BuildRuns(text.Value, scope, style) };
    }

    private Node LowerMarkdown(string markdown, IDictionary<string, object?> scope)
        => new ColumnNode { Spacing = 4, Children = MarkdownLowering.Parse(Interpolate(markdown, scope)) };

    private Node LowerRow(RowBlock row, IDictionary<string, object?> scope, IReadOnlyDictionary<string, IReadOnlyList<Node>> slots)
        => new RowNode
        {
            Spacing = row.Spacing,
            Items = row.Items
                .Select(item => new RowItem { Width = ParseWidth(item.Width), Child = LowerBlock(item, scope, slots) ?? new TextNode() })
                .ToList(),
        };

    private Node LowerColumn(ColumnBlock column, IDictionary<string, object?> scope, IReadOnlyDictionary<string, IReadOnlyList<Node>> slots)
        => new ColumnNode { Spacing = column.Spacing, Children = LowerBlocks(column.Content, scope, slots) };

    private Node LowerCard(CardBlock card, IDictionary<string, object?> scope, IReadOnlyDictionary<string, IReadOnlyList<Node>> slots)
        => new ContainerNode { Style = MapStyle(card.Style, card.Class), Children = LowerBlocks(card.Content, scope, slots) };

    private Node LowerComponentUse(string name, Dictionary<string, string>? with, IDictionary<string, object?> callerScope)
    {
        var component = ResolveComponent(name);
        var componentScope = BuildComponentScope(component, with, callerScope);
        return new ColumnNode { Children = LowerBlocks(component.Body, componentScope, NoSlots) };
    }

    private ComponentModule ResolveComponent(string name)
        => _registry.TryGetComponent(name, out var component)
            ? component
            : throw new InvalidOperationException($"Component '{name}' was not found among the imported modules.");

    /// <summary>Component scope = declared params (from <c>with</c> or defaults) + minimal ambient. No <c>data</c> (blueprint §7).</summary>
    private Dictionary<string, object?> BuildComponentScope(
        ComponentModule component, Dictionary<string, string>? with, IDictionary<string, object?> callerScope)
    {
        var scope = new Dictionary<string, object?>
        {
            ["now"] = DateTime.Now,
            ["today"] = DateTime.Today,
        };

        foreach (var (name, def) in component.Params)
        {
            var raw = with?.GetValueOrDefault(name) ?? def.Default;
            scope[name] = raw is null ? null : Interpolate(raw, callerScope);
        }
        return scope;
    }

    private IReadOnlyList<Run> BuildRuns(string? template, IDictionary<string, object?> scope, Style style)
    {
        var runs = new List<Run>();
        if (string.IsNullOrEmpty(template))
        {
            runs.Add(new Run { Text = string.Empty, Style = style });
            return runs;
        }

        var last = 0;
        foreach (Match match in TokenRegex().Matches(template))
        {
            if (match.Index > last)
                runs.Add(new Run { Text = template[last..match.Index], Style = style });

            var expr = match.Groups[1].Value.Trim();
            runs.Add(expr switch
            {
                "page" => new Run { Dynamic = RunDynamic.PageNumber, Style = style },
                "pageCount" => new Run { Dynamic = RunDynamic.TotalPages, Style = style },
                _ => new Run { Text = ExpressionValues.Stringify(Evaluate(expr, scope)), Style = style },
            });
            last = match.Index + match.Length;
        }

        if (last < template.Length)
            runs.Add(new Run { Text = template[last..], Style = style });

        if (runs.Count == 0)
            runs.Add(new Run { Text = string.Empty, Style = style });

        return runs;
    }

    // ── Table (blueprint §5) ───────────────────────────────────────────────────

    private TableNode LowerTable(TableBlock table, IDictionary<string, object?> rootScope)
    {
        var columns = table.Columns
            .Select(c => new TableColumn { Width = ParseWidth(c.Width), Header = c.Header })
            .ToList();

        var items = ExpressionValues.AsEnumerable(Evaluate(table.Data, rootScope)).ToList();
        var rowStyle = MapRowStyle(table.RowStyle);

        var sections = string.IsNullOrWhiteSpace(table.GroupBy)
            ? [new TableSection { Rows = BuildRows(items, table.Columns, rootScope, rowStyle) }]
            : BuildGroupedSections(items, table, columns.Count, rootScope, rowStyle);

        var footer = table.Footer
            .Select(fc => new TableCell
            {
                Text = Interpolate(fc.Text, rootScope),
                Span = Math.Max(1, fc.Span),
                Style = MapCellStyle(fc.Style, fc.Align, fc.Class),
            })
            .ToList();

        return new TableNode { Columns = columns, Sections = sections, Footer = footer };
    }

    private List<TableSection> BuildGroupedSections(
        List<object?> items, TableBlock table, int columnCount,
        IDictionary<string, object?> rootScope, Style rowStyle)
    {
        var order = new List<string>();
        var buckets = new Dictionary<string, List<object?>>();
        foreach (var item in items)
        {
            var key = ExpressionValues.Stringify(ExpressionValues.GetMember(item, table.GroupBy!));
            if (!buckets.TryGetValue(key, out var bucket))
            {
                buckets[key] = bucket = [];
                order.Add(key);
            }
            bucket.Add(item);
        }

        var sections = new List<TableSection>(order.Count);
        foreach (var key in order)
        {
            var groupItems = buckets[key];
            var groupScope = new Dictionary<string, object?>(rootScope)
            {
                ["group"] = new Dictionary<string, object?> { ["key"] = key, ["items"] = groupItems },
            };

            sections.Add(new TableSection
            {
                Header = table.Group?.Header is { } h ? BandCell(h, groupScope, columnCount) : null,
                Rows = BuildRows(groupItems, table.Columns, rootScope, rowStyle),
                Footer = table.Group?.Footer is { } f ? BandCell(f, groupScope, columnCount) : null,
            });
        }
        return sections;
    }

    private List<TableRow> BuildRows(
        List<object?> items, List<ColumnDef> columns,
        IDictionary<string, object?> rootScope, Style rowStyle)
    {
        var rows = new List<TableRow>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            // The item's own fields are exposed at top level (so `{{ nome }}` works, and lib
            // expressions like `price * (1 - desconto)` resolve) alongside `item`/index/first/last.
            var rowScope = new Dictionary<string, object?>(rootScope);
            foreach (var (key, value) in ExpressionValues.ToScope(items[i]))
                rowScope[key] = value;
            rowScope["item"] = items[i];
            rowScope["index"] = (double)i;
            rowScope["first"] = i == 0;
            rowScope["last"] = i == items.Count - 1;

            var cells = columns
                .Select(c => new TableCell
                {
                    Text = Interpolate(c.Cell, rowScope),
                    Style = MapCellStyle(c.Style, c.Align, c.Class),
                })
                .ToList();

            rows.Add(new TableRow { Cells = cells, RowStyle = rowStyle });
        }
        return rows;
    }

    private TableCell BandCell(GroupBandDef band, IDictionary<string, object?> scope, int span)
        => new()
        {
            Text = Interpolate(band.Text, scope),
            Span = Math.Max(1, span),
            Style = MapCellStyle(band.Style, null, band.Class),
        };

    // ── Style mapping (precedence: theme < class < inline) ─────────────────────

    private Style MapStyle(StyleDef? inline, string? className)
        => MapStyleDef(ResolveStyleDef(inline, className));

    private Style MapCellStyle(StyleDef? inline, string? align, string? className)
    {
        var def = ResolveStyleDef(inline, className);
        return new Style
        {
            Color = def?.Color,
            Background = def?.Background,
            Bold = def?.Bold,
            Italic = def?.Italic,
            Size = def?.Size,
            Align = ParseAlign(align) ?? ParseAlign(def?.Align),
        };
    }

    private StyleDef? ResolveStyleDef(StyleDef? inline, string? className)
    {
        if (!_registry.TryGetClass(className, out var classStyle))
            return inline;
        return inline is null ? classStyle : ModuleRegistry.MergeStyleDefs(classStyle, inline);
    }

    private static Style MapStyleDef(StyleDef? def)
    {
        if (def is null)
            return new Style();

        var (borderWidth, borderColor) = ParseBorder(def.Border);
        return new Style
        {
            Color = def.Color,
            Background = def.Background,
            Bold = def.Bold,
            Italic = def.Italic,
            Size = def.Size,
            Align = ParseAlign(def.Align),
            Padding = def.Padding,
            BorderWidth = borderWidth,
            BorderColor = borderColor,
        };
    }

    private static Style MapRowStyle(StyleDef? def)
    {
        if (def is null)
            return new Style();

        var (borderWidth, borderColor) = ParseBorder(def.BorderBottom);
        return new Style
        {
            Background = def.Background,
            PaddingVertical = def.PaddingY,
            BorderBottomWidth = borderWidth,
            BorderBottomColor = borderColor,
        };
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private string Interpolate(string? template, IDictionary<string, object?> scope)
        => ExpressionEvaluator.Interpolate(template, scope, _context);

    private object? Evaluate(string expression, IDictionary<string, object?> scope)
        => ExpressionEvaluator.Evaluate(expression, scope, _context);

    private static TextAlign? ParseAlign(string? align) => align?.Trim().ToLowerInvariant() switch
    {
        "left" => TextAlign.Left,
        "center" => TextAlign.Center,
        "right" => TextAlign.Right,
        "justify" => TextAlign.Justify,
        _ => null,
    };

    private static ImageFit ParseImageFit(string? fit) => fit?.Trim().ToLowerInvariant() switch
    {
        "height" => ImageFit.Height,
        "area" => ImageFit.Area,
        "unproportional" => ImageFit.Unproportional,
        _ => ImageFit.Width,
    };

    private static Node LowerImage(ImageBlock image) => new ImageNode
    {
        Data = string.IsNullOrWhiteSpace(image.Base64) ? null : DecodeBase64(image.Base64),
        Url = image.Url,
        Fit = ParseImageFit(image.Fit),
        Width = image.Width,
        Height = image.Height,
    };

    private static Node LowerDivider(DividerBlock divider)
        => new DividerNode { Style = new Style { BorderWidth = divider.Thickness ?? 1f, BorderColor = divider.Color } };

    private static byte[]? DecodeBase64(string value)
    {
        var comma = value.IndexOf(',');
        var payload = value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0
            ? value[(comma + 1)..]
            : value;
        try
        {
            return Convert.FromBase64String(payload.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static (float? Width, string? Color) ParseBorder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (null, null);

        float? width = null;
        string? color = null;
        foreach (var part in value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith('#'))
                color = part;
            else if (TryParseNumber(part, out var n))
                width = n;
        }

        if (color is not null && width is null)
            width = 1f;
        return (width, color);
    }

    private PageSettings MapPage(PageDef? reportPage)
    {
        var settings = PageSettings.Default();
        var theme = _registry.ThemePage;

        var size = FirstNonEmpty(reportPage?.Size, theme?.Size);
        if (size is not null)
            settings.PageSize = size;

        var margin = FirstNonEmpty(reportPage?.Margin, theme?.Margin);
        if (TryParseNumber(margin, out var cm))
        {
            settings.MarginHorizontal = cm;
            settings.MarginVertical = cm;
        }

        return settings;
    }

    private static string? FirstNonEmpty(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) ? a : (!string.IsNullOrWhiteSpace(b) ? b : null);

    private const float PointsPerCentimetre = 28.3465f;

    private static ColumnWidth ParseWidth(string? width)
    {
        if (string.IsNullOrWhiteSpace(width) || width.Trim() == "*")
            return ColumnWidth.Relative(1);

        var w = width.Trim();

        if (w.EndsWith('*'))
            return ColumnWidth.Relative(TryParseNumber(w[..^1], out var rel) ? rel : 1f);

        if (w.EndsWith('%'))
            return ColumnWidth.Relative(TryParseNumber(w[..^1], out var pct) ? pct : 1f);

        if (w.EndsWith("cm", StringComparison.OrdinalIgnoreCase))
            return ColumnWidth.Constant((TryParseNumber(w[..^2], out var cm) ? cm : 0f) * PointsPerCentimetre);

        if (w.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            return ColumnWidth.Constant(TryParseNumber(w[..^2], out var px) ? px : 0f);

        return ColumnWidth.Constant(TryParseNumber(w, out var pt) ? pt : 0f);
    }

    private static bool TryParseNumber(string? value, out float number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var numeric = new string(value.Trim().TakeWhile(c => char.IsDigit(c) || c is '.' or ',').ToArray())
            .Replace(',', '.');

        return float.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }
}
