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
            name: invoice
            content:
              - text: { value: "Items" }
              - table:
                  data: data.items
                  columns:
                    - { header: "Product", cell: "{{ item.name }}" }
                    - { header: "Total",   cell: "{{ currency(item.price) }}" }
                  footer:
                    - { span: 1, text: "Total" }
                    - { text: "{{ currency(sum(data.items, 'price')) }}" }
            """;
        var data = Data(new { items = new[] { new { name = "Table", price = 100 }, new { name = "Chair", price = 50 } } });

        var bytes = CreateEngine().RenderExcel(yaml, data);

        // .xlsx is a zip container → starts with "PK".
        Assert.Equal(new byte[] { 0x50, 0x4B }, bytes.AsSpan(0, 2).ToArray());

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Items", sheet.Cell(1, 1).GetString());      // text node
        Assert.Equal("Product", sheet.Cell(2, 1).GetString());    // table header
        Assert.Equal("Table", sheet.Cell(3, 1).GetString());      // row 1
        Assert.Equal("Chair", sheet.Cell(4, 1).GetString());      // row 2
        Assert.Contains("150", sheet.Cell(5, 2).GetString());     // footer aggregate
    }
}
