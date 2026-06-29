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
              title: { extends: base, size: 18, bold: true }
            """;
        const string report = """
            kind: report
            name: r
            content:
              - text: { value: "T", class: title }
              - text: { value: "I", class: title, style: { color: "#FF0000" } }
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
              - text: { value: "{{ data.code | doc4 }}" }
            """;

        var document = CreateEngine().Build(report, Data(new { code = "1234" }), [formats]);

        Assert.Equal("12-34", Concat(Assert.IsType<TextNode>(Assert.Single(document.Page.Content))));
    }

    [Fact]
    public void Lib_named_expression_resolves_in_row_scope()
    {
        const string lib = """
            kind: lib
            name: sales
            expr:
              finalPrice: "{{ price * (1 - discount) }}"
            """;
        const string report = """
            kind: report
            name: r
            content:
              - table:
                  data: data.items
                  columns:
                    - { header: "Final", cell: "{{ sales.finalPrice }}" }
            """;

        var document = CreateEngine().Build(report, Data(new { items = new[] { new { price = 100, discount = 0.1 } } }), [lib]);

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
              title: { size: 20, bold: true }
            """;
        const string report = """
            kind: report
            name: r
            content:
              - text: { value: "T", class: title }
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
            name: defaultLayout
            params:
              title: { type: string }
              company: { type: string, default: "Contar" }
            slots: [content]
            body:
              - text: { value: "{{ company }} — {{ title }}", style: { bold: true } }
              - divider: {}
              - slot: content
            """;
        const string report = """
            kind: report
            name: r
            use: defaultLayout
            with: { title: "Report X" }
            content:
              - text: { value: "body" }
            """;

        var document = CreateEngine().Build(report, Data(new { }), [component]);

        Assert.Equal(3, document.Page.Content.Count);
        Assert.Equal("Contar — Report X", Concat(Assert.IsType<TextNode>(document.Page.Content[0])));
        Assert.IsType<DividerNode>(document.Page.Content[1]);

        var slot = Assert.IsType<ColumnNode>(document.Page.Content[2]);
        Assert.Equal("body", Concat(Assert.IsType<TextNode>(Assert.Single(slot.Children))));
    }

    [Fact]
    public void Component_block_level_use_with_params()
    {
        const string component = """
            kind: component
            name: header
            params:
              company: { type: string }
            body:
              - text: { value: "Company: {{ company }}" }
            """;
        const string report = """
            kind: report
            name: r
            content:
              - use: header
                with: { company: "ACME" }
            """;

        var document = CreateEngine().Build(report, Data(new { }), [component]);

        var wrap = Assert.IsType<ColumnNode>(Assert.Single(document.Page.Content));
        Assert.Equal("Company: ACME", Concat(Assert.IsType<TextNode>(Assert.Single(wrap.Children))));
    }

    [Fact]
    public void Duplicate_style_class_across_modules_throws()
    {
        const string a = "kind: styles\nname: a\nclasses:\n  title: { size: 1 }";
        const string b = "kind: styles\nname: b\nclasses:\n  title: { size: 2 }";
        const string report = "kind: report\nname: r\ncontent: []";

        Assert.Throws<InvalidOperationException>(() => CreateEngine().Build(report, Data(new { }), [a, b]));
    }

    [Fact]
    public void Renders_with_modules_to_pdf()
    {
        const string styles = "kind: styles\nname: s\nclasses:\n  title: { size: 18, bold: true }";
        const string component = """
            kind: component
            name: layout
            params: { title: { type: string } }
            slots: [content]
            body:
              - text: { value: "{{ title }}", class: title }
              - slot: content
            """;
        const string report = """
            kind: report
            name: r
            use: layout
            with: { title: "Report" }
            content:
              - text: { value: "body" }
            """;

        var bytes = CreateEngine().RenderPdf(report, Data(new { }), [styles, component]);
        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }
}
