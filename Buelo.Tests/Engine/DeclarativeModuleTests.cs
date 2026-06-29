using System.Text.Json;
using Buelo.Engine.Declarative;
using Buelo.Engine.Ir;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class DeclarativeModuleTests
{
    public DeclarativeModuleTests()
    {
        Settings.License = LicenseType.Community;
    }

    private static DeclarativeReportEngine CreateEngine() => new(new DeclarativeInterpreter());

    private static JsonElement Data(object value)
        => JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));

    private static string Concat(TextNode node) => string.Concat(node.Runs.Select(r => r.Text));

    [Fact]
    public void Styles_class_extends_and_inline_precedence()
    {
        const string styles = """
            kind: styles
            name: corp
            classes:
              base: { size: 12, color: "#333333" }
              titulo: { extends: base, size: 18, bold: true }
            """;
        const string report = """
            kind: report
            name: r
            content:
              - text: { value: "T", class: titulo }
              - text: { value: "I", class: titulo, style: { color: "#FF0000" } }
            """;

        var document = CreateEngine().Build(report, Data(new { }), [styles]);

        var fromClass = Assert.IsType<TextNode>(document.Page.Content[0]);
        Assert.Equal(18f, fromClass.Style.Size);
        Assert.True(fromClass.Style.Bold);
        Assert.Equal("#333333", fromClass.Style.Color);   // inherited via extends

        var inlineWins = Assert.IsType<TextNode>(document.Page.Content[1]);
        Assert.Equal("#FF0000", inlineWins.Style.Color);  // inline overrides class
        Assert.Equal(18f, inlineWins.Style.Size);
        Assert.True(inlineWins.Style.Bold);
    }

    [Fact]
    public void Custom_format_used_as_pipe()
    {
        const string formats = """
            kind: formats
            name: br
            formats:
              doc4: "##-##"
            """;
        const string report = """
            kind: report
            name: r
            content:
              - text: { value: "{{ data.codigo | doc4 }}" }
            """;

        var document = CreateEngine().Build(report, Data(new { codigo = "1234" }), [formats]);

        Assert.Equal("12-34", Concat(Assert.IsType<TextNode>(Assert.Single(document.Page.Content))));
    }

    [Fact]
    public void Lib_named_expression_resolves_in_row_scope()
    {
        const string lib = """
            kind: lib
            name: vendas
            expr:
              precoFinal: "{{ price * (1 - desconto) }}"
            """;
        const string report = """
            kind: report
            name: r
            content:
              - table:
                  data: data.itens
                  columns:
                    - { header: "Final", cell: "{{ vendas.precoFinal }}" }
            """;

        var document = CreateEngine().Build(report, Data(new { itens = new[] { new { price = 100, desconto = 0.1 } } }), [lib]);

        var table = Assert.IsType<TableNode>(Assert.Single(document.Page.Content));
        Assert.Equal("90", table.Sections[0].Rows[0].Cells[0].Text);
    }

    [Fact]
    public void Theme_provides_page_defaults_and_classes()
    {
        const string theme = """
            kind: theme
            name: corp
            page: { size: Letter, margin: "3cm" }
            styles:
              titulo: { size: 20, bold: true }
            """;
        const string report = """
            kind: report
            name: r
            content:
              - text: { value: "T", class: titulo }
            """;

        var document = CreateEngine().Build(report, Data(new { }), [theme]);

        Assert.Equal("Letter", document.Meta.PageSettings.PageSize);
        Assert.Equal(3f, document.Meta.PageSettings.MarginHorizontal);
        var text = Assert.IsType<TextNode>(Assert.Single(document.Page.Content));
        Assert.Equal(20f, text.Style.Size);
        Assert.True(text.Style.Bold);
    }

    [Fact]
    public void Component_layout_wrap_fills_content_slot()
    {
        const string component = """
            kind: component
            name: layoutPadrao
            params:
              titulo: { type: string }
              empresa: { type: string, default: "Contar" }
            slots: [content]
            body:
              - text: { value: "{{ empresa }} — {{ titulo }}", style: { bold: true } }
              - divider: {}
              - slot: content
            """;
        const string report = """
            kind: report
            name: r
            use: layoutPadrao
            with: { titulo: "Relatório X" }
            content:
              - text: { value: "corpo" }
            """;

        var document = CreateEngine().Build(report, Data(new { }), [component]);

        Assert.Equal(3, document.Page.Content.Count);
        Assert.Equal("Contar — Relatório X", Concat(Assert.IsType<TextNode>(document.Page.Content[0])));
        Assert.IsType<DividerNode>(document.Page.Content[1]);

        var slot = Assert.IsType<ColumnNode>(document.Page.Content[2]);
        Assert.Equal("corpo", Concat(Assert.IsType<TextNode>(Assert.Single(slot.Children))));
    }

    [Fact]
    public void Component_block_level_use_with_params()
    {
        const string component = """
            kind: component
            name: cabecalho
            params:
              empresa: { type: string }
            body:
              - text: { value: "Empresa: {{ empresa }}" }
            """;
        const string report = """
            kind: report
            name: r
            content:
              - use: cabecalho
                with: { empresa: "ACME" }
            """;

        var document = CreateEngine().Build(report, Data(new { }), [component]);

        var wrap = Assert.IsType<ColumnNode>(Assert.Single(document.Page.Content));
        Assert.Equal("Empresa: ACME", Concat(Assert.IsType<TextNode>(Assert.Single(wrap.Children))));
    }

    [Fact]
    public void Duplicate_style_class_across_modules_throws()
    {
        const string a = "kind: styles\nname: a\nclasses:\n  titulo: { size: 1 }";
        const string b = "kind: styles\nname: b\nclasses:\n  titulo: { size: 2 }";
        const string report = "kind: report\nname: r\ncontent: []";

        Assert.Throws<InvalidOperationException>(() => CreateEngine().Build(report, Data(new { }), [a, b]));
    }

    [Fact]
    public void Renders_with_modules_to_pdf()
    {
        const string styles = "kind: styles\nname: s\nclasses:\n  titulo: { size: 18, bold: true }";
        const string component = """
            kind: component
            name: layout
            params: { titulo: { type: string } }
            slots: [content]
            body:
              - text: { value: "{{ titulo }}", class: titulo }
              - slot: content
            """;
        const string report = """
            kind: report
            name: r
            use: layout
            with: { titulo: "Relatório" }
            content:
              - text: { value: "corpo" }
            """;

        var bytes = CreateEngine().RenderPdf(report, Data(new { }), [styles, component]);
        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }
}
