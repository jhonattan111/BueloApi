using System.Text.Json;
using Buelo.Contracts;
using Buelo.Engine;
using Buelo.Engine.Declarative;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class DeclarativeEjectTests
{
    public DeclarativeEjectTests()
    {
        Settings.License = LicenseType.Community;
    }

    private static DeclarativeReportEngine CreateEngine() => new(new DeclarativeInterpreter());

    private static JsonElement Data(object value)
        => JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));

    private const string ReportYaml = """
        kind: report
        name: fatura
        header:
          - text: { value: "Fatura {{ data.numero }}", style: { bold: true, size: 16 } }
          - divider: {}
        content:
          - table:
              data: data.itens
              columns:
                - { width: 3*, header: "Produto", cell: "{{ item.nome }}" }
                - { width: 1*, header: "Total", cell: "{{ moeda(item.preco) }}", align: right }
              footer:
                - { span: 1, text: "Total" }
                - { text: "{{ moeda(sum(data.itens, 'preco')) }}" }
        footer:
          - text: { value: "Página {{ page }} de {{ pageCount }}", style: { align: center } }
        """;

    private static JsonElement InvoiceData() => Data(new
    {
        numero = 42,
        itens = new[] { new { nome = "Mesa", preco = 100 }, new { nome = "Cadeira", preco = 50 } },
    });

    [Fact]
    public void Eject_produces_idocument_source()
    {
        var source = CreateEngine().EjectCSharp(ReportYaml, InvoiceData());

        Assert.Contains(": IDocument", source);
        Assert.Contains("container.Page(", source);
        Assert.Contains(".Table(", source);
        Assert.Contains("CurrentPageNumber()", source);
    }

    [Fact]
    public async Task Ejected_source_compiles_and_renders_via_roslyn()
    {
        // Round-trip: declarative → IR → ejected C# → Roslyn compile → QuestPDF render.
        var source = CreateEngine().EjectCSharp(ReportYaml, InvoiceData());

        var templateEngine = new TemplateEngine(new DefaultHelperRegistry());
        var pdf = await templateEngine.RenderAsync(source, Data(new { }), TemplateMode.FullClass);

        Assert.Equal("%PDF"u8.ToArray(), pdf.AsSpan(0, 4).ToArray());
    }
}
