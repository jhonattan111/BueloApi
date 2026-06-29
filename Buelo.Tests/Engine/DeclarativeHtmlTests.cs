using System.Text.Json;
using Buelo.Engine.Declarative;
using Buelo.Engine.Ir;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class DeclarativeHtmlTests
{
    public DeclarativeHtmlTests()
    {
        Settings.License = LicenseType.Community;
    }

    [Fact]
    public void Parses_headings_paragraph_list_and_inline()
    {
        var nodes = HtmlLowering.Parse(
            "<h1>Título</h1><p>Texto com <b>negrito</b> e <i>itálico</i>.</p><ul><li>um</li><li>dois</li></ul>");

        Assert.Equal(4, nodes.Count); // h1, p, li, li

        var heading = Assert.IsType<TextNode>(nodes[0]);
        Assert.True(heading.Style.Bold);
        Assert.Equal(22f, heading.Style.Size);
        Assert.Equal("Título", string.Concat(heading.Runs.Select(r => r.Text)));

        var paragraph = Assert.IsType<TextNode>(nodes[1]);
        Assert.Contains(paragraph.Runs, r => r.Text == "negrito" && r.Style.Bold == true);
        Assert.Contains(paragraph.Runs, r => r.Text == "itálico" && r.Style.Italic == true);

        var bullet = Assert.IsType<TextNode>(nodes[2]);
        Assert.StartsWith("•", bullet.Runs[0].Text);
    }

    [Fact]
    public void Html_block_renders_to_pdf()
    {
        var engine = new DeclarativeReportEngine(new DeclarativeInterpreter());
        const string yaml = """
            kind: report
            name: r
            content:
              - html: "<h2>{{ data.titulo }}</h2><p>Olá <b>mundo</b></p>"
            """;
        var data = JsonSerializer.Deserialize<JsonElement>("""{ "titulo": "Relatório" }""");

        var bytes = engine.RenderPdf(yaml, data);

        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }
}
