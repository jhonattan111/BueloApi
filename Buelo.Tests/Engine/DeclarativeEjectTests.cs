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
        name: invoice
        header:
          - text: { value: "Invoice {{ data.number }}", style: { bold: true, size: 16 } }
          - divider: {}
        content:
          - table:
              data: data.items
              columns:
                - { width: 3*, header: "Product", cell: "{{ item.name }}" }
                - { width: 1*, header: "Total", cell: "{{ currency(item.price) }}", align: right }
              footer:
                - { span: 1, text: "Total" }
                - { text: "{{ currency(sum(data.items, 'price')) }}" }
        footer:
          - text: { value: "Page {{ page }} of {{ pageCount }}", style: { align: center } }
        """;

    private static JsonElement InvoiceData() => Data(new
    {
        number = 42,
        items = new[] { new { name = "Table", price = 100 }, new { name = "Chair", price = 50 } },
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
