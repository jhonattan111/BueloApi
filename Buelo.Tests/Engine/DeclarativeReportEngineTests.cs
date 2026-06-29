using System.Text.Json;
using Buelo.Engine.Declarative;
using Buelo.Engine.Ir;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class DeclarativeReportEngineTests
{
    public DeclarativeReportEngineTests()
    {
        Settings.License = LicenseType.Community;
    }

    private static DeclarativeReportEngine CreateEngine() => new(new DeclarativeInterpreter());

    private static JsonElement Data(object value)
        => JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));

    [Fact]
    public void Build_ResolvesDataExpression_IntoStyledTextRun()
    {
        const string yaml = """
            kind: report
            name: hello
            meta:
              engine: declarative
              recipe: pdf
            content:
              - text: { value: "Hello {{ data.name }}", style: { bold: true, size: 18 } }
            """;

        var document = CreateEngine().Build(yaml, Data(new { name = "Buelo" }));

        var text = Assert.IsType<TextNode>(Assert.Single(document.Page.Content));
        Assert.Equal("Hello Buelo", string.Concat(text.Runs.Select(r => r.Text)));
        Assert.True(text.Style.Bold);
        Assert.Equal(18f, text.Style.Size);
    }

    [Fact]
    public void Build_ResolvesNestedPath()
    {
        const string yaml = """
            kind: report
            name: nested
            content:
              - text: { value: "Client: {{ data.client.name }}" }
            """;

        var document = CreateEngine().Build(yaml, Data(new { client = new { name = "Contar" } }));

        var text = Assert.IsType<TextNode>(Assert.Single(document.Page.Content));
        Assert.Equal("Client: Contar", string.Concat(text.Runs.Select(r => r.Text)));
    }

    [Fact]
    public void Build_UnknownPath_ResolvesToEmpty()
    {
        const string yaml = """
            kind: report
            name: missing
            content:
              - text: { value: "Value: {{ data.nonexistent }}" }
            """;

        var document = CreateEngine().Build(yaml, Data(new { name = "x" }));

        var text = Assert.IsType<TextNode>(Assert.Single(document.Page.Content));
        Assert.Equal("Value: ", string.Concat(text.Runs.Select(r => r.Text)));
    }

    [Fact]
    public void Build_MapsPageSettings()
    {
        const string yaml = """
            kind: report
            name: paged
            meta:
              page: { size: Letter, margin: "3cm" }
            content:
              - text: { value: "x" }
            """;

        var document = CreateEngine().Build(yaml, Data(new { }));

        Assert.Equal("Letter", document.Meta.PageSettings.PageSize);
        Assert.Equal(3f, document.Meta.PageSettings.MarginHorizontal);
        Assert.Equal(3f, document.Meta.PageSettings.MarginVertical);
    }

    [Fact]
    public void RenderPdf_ProducesNonEmptyPdf()
    {
        const string yaml = """
            kind: report
            name: hello
            content:
              - text: { value: "Hello {{ data.name }}" }
            """;

        var bytes = CreateEngine().RenderPdf(yaml, Data(new { name = "Buelo" }));

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }

    [Fact]
    public void Parse_InvalidYaml_Throws()
    {
        const string badYaml = "kind: report\ncontent: [ unterminated";

        Assert.Throws<InvalidOperationException>(() => CreateEngine().Build(badYaml, null));
    }
}
