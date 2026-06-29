using System.Text.Json;
using Buelo.Engine.Declarative;
using Buelo.Engine.Ir;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class DeclarativeLayoutTests
{
    public DeclarativeLayoutTests()
    {
        Settings.License = LicenseType.Community;
    }

    private static DeclarativeReportEngine CreateEngine() => new(new DeclarativeInterpreter());

    private static JsonElement Data(object value)
        => JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));

    private const string FullReportYaml = """
        kind: report
        name: completo
        meta:
          page: { size: A4, margin: 2cm, orientation: portrait }
        header:
          - text: { value: "Relatório {{ report.name }}", style: { bold: true, size: 16 } }
          - divider: { color: "#CCCCCC", thickness: 1 }
        content:
          - markdown: "# Título\n\nTexto com **negrito**.\n\n- item 1\n- item 2"
          - spacer: 10
          - row:
              spacing: 8
              items:
                - width: 2*
                  card:
                    style: { background: "#F5F5F5", padding: 8, border: "1px #DDD" }
                    content:
                      - text: { value: "Coluna esquerda" }
                - width: 1*
                  column:
                    content:
                      - text: { value: "A" }
                      - text: { value: "B" }
          - pageBreak: true
          - text: { value: "Página 2" }
        footer:
          - text: { value: "Página {{ page }} de {{ pageCount }}", style: { align: center } }
        """;

    [Fact]
    public void Lowers_bands_and_layout_blocks()
    {
        var document = CreateEngine().Build(FullReportYaml, Data(new { }));

        Assert.Equal("portrait", document.Meta.Orientation);

        // Header: text + divider
        Assert.Equal(2, document.Page.Header.Count);
        var title = Assert.IsType<TextNode>(document.Page.Header[0]);
        Assert.Equal("Relatório completo", string.Concat(title.Runs.Select(r => r.Text)));
        Assert.IsType<DividerNode>(document.Page.Header[1]);

        // Content: markdown(Column) + spacer + row + pageBreak + text
        Assert.Equal(5, document.Page.Content.Count);
        Assert.IsType<ColumnNode>(document.Page.Content[0]);
        Assert.IsType<SpacerNode>(document.Page.Content[1]);
        Assert.IsType<PageBreakNode>(document.Page.Content[3]);

        var row = Assert.IsType<RowNode>(document.Page.Content[2]);
        Assert.Equal(2, row.Items.Count);
        Assert.Equal(2f, row.Items[0].Width.Value);
        Assert.IsType<ContainerNode>(row.Items[0].Child);
        Assert.IsType<ColumnNode>(row.Items[1].Child);
    }

    [Fact]
    public void Footer_emits_dynamic_page_runs()
    {
        var document = CreateEngine().Build(FullReportYaml, Data(new { }));

        var footer = Assert.IsType<TextNode>(Assert.Single(document.Page.Footer));
        Assert.Contains(footer.Runs, r => r.Dynamic == RunDynamic.PageNumber);
        Assert.Contains(footer.Runs, r => r.Dynamic == RunDynamic.TotalPages);
    }

    [Fact]
    public void Card_resolves_box_style()
    {
        var document = CreateEngine().Build(FullReportYaml, Data(new { }));

        var row = Assert.IsType<RowNode>(document.Page.Content[2]);
        var card = Assert.IsType<ContainerNode>(row.Items[0].Child);
        Assert.Equal("#F5F5F5", card.Style.Background);
        Assert.Equal(8f, card.Style.Padding);
        Assert.Equal(1f, card.Style.BorderWidth);
        Assert.Equal("#DDD", card.Style.BorderColor);
    }

    [Fact]
    public void Markdown_parses_headings_and_inline()
    {
        var nodes = MarkdownLowering.Parse("# Título\n\nTexto com **negrito** aqui.\n\n- item");

        var heading = Assert.IsType<TextNode>(nodes[0]);
        Assert.True(heading.Style.Bold);
        Assert.Equal(22f, heading.Style.Size);
        Assert.Equal("Título", string.Concat(heading.Runs.Select(r => r.Text)));

        var paragraph = Assert.IsType<TextNode>(nodes[1]);
        Assert.Contains(paragraph.Runs, r => r.Text == "negrito" && r.Style.Bold == true);

        var bullet = Assert.IsType<TextNode>(nodes[2]);
        Assert.StartsWith("•", bullet.Runs[0].Text);
    }

    [Fact]
    public void Renders_full_report_to_pdf()
    {
        var bytes = CreateEngine().RenderPdf(FullReportYaml, Data(new { }));

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }

    [Fact]
    public void Renders_landscape_orientation()
    {
        const string yaml = """
            kind: report
            name: paisagem
            meta:
              page: { size: A4, orientation: landscape }
            content:
              - text: { value: "Wide" }
            """;

        var bytes = CreateEngine().RenderPdf(yaml, Data(new { }));
        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }
}
