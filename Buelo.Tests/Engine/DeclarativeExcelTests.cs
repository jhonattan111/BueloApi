using System.Text.Json;
using Buelo.Engine.Declarative;
using ClosedXML.Excel;

namespace Buelo.Tests.Engine;

public class DeclarativeExcelTests
{
    private static DeclarativeReportEngine CreateEngine() => new(new DeclarativeInterpreter());

    private static JsonElement Data(object value)
        => JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));

    [Fact]
    public void RenderExcel_table_produces_workbook_with_header_and_rows()
    {
        const string yaml = """
            kind: report
            name: fatura
            content:
              - text: { value: "Itens" }
              - table:
                  data: data.itens
                  columns:
                    - { header: "Produto", cell: "{{ item.nome }}" }
                    - { header: "Total",   cell: "{{ moeda(item.preco) }}" }
                  footer:
                    - { span: 1, text: "Total" }
                    - { text: "{{ moeda(sum(data.itens, 'preco')) }}" }
            """;
        var data = Data(new { itens = new[] { new { nome = "Mesa", preco = 100 }, new { nome = "Cadeira", preco = 50 } } });

        var bytes = CreateEngine().RenderExcel(yaml, data);

        // .xlsx is a zip container → starts with "PK".
        Assert.Equal(new byte[] { 0x50, 0x4B }, bytes.AsSpan(0, 2).ToArray());

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Itens", sheet.Cell(1, 1).GetString());      // text node
        Assert.Equal("Produto", sheet.Cell(2, 1).GetString());    // table header
        Assert.Equal("Mesa", sheet.Cell(3, 1).GetString());       // row 1
        Assert.Equal("Cadeira", sheet.Cell(4, 1).GetString());    // row 2
        Assert.Contains("150", sheet.Cell(5, 2).GetString());     // footer aggregate
    }
}
