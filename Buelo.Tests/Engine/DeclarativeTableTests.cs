using System.Text.Json;
using Buelo.Engine.Declarative;
using Buelo.Engine.Ir;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class DeclarativeTableTests
{
    public DeclarativeTableTests()
    {
        Settings.License = LicenseType.Community;
    }

    private static DeclarativeReportEngine CreateEngine() => new(new DeclarativeInterpreter());

    private static JsonElement Data(object value)
        => JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));

    private const string InvoiceYaml = """
        kind: report
        name: invoice
        content:
          - table:
              data: data.items
              rowStyle: { paddingY: 4, borderBottom: "1px #DDD" }
              columns:
                - { width: 25px, header: "#",        cell: "{{ index + 1 }}" }
                - { width: 3*,   header: "Product",  cell: "{{ item.name }}" }
                - { width: 1*,   header: "Total",    cell: "{{ currency(item.price * item.qty) }}", align: right }
              footer:
                - { span: 2, text: "Total", style: { bold: true, align: right } }
                - { text: "{{ currency(sum(data.items, 'price * qty')) }}", align: right }
        """;

    private static JsonElement InvoiceData() => Data(new
    {
        items = new[]
        {
            new { name = "Chair", price = 10, qty = 2 },
            new { name = "Table", price = 5, qty = 3 },
        },
    });

    [Fact]
    public void Table_LowersRowsWithRowContext()
    {
        var document = CreateEngine().Build(InvoiceYaml, InvoiceData());

        var table = Assert.IsType<TableNode>(Assert.Single(document.Page.Content));
        Assert.Equal(3, table.Columns.Count);
        Assert.Equal(ColumnWidthKind.Constant, table.Columns[0].Width.Kind);
        Assert.Equal(25f, table.Columns[0].Width.Value);
        Assert.Equal(ColumnWidthKind.Relative, table.Columns[1].Width.Kind);
        Assert.Equal(3f, table.Columns[1].Width.Value);

        var rows = Assert.Single(table.Sections).Rows;
        Assert.Equal(2, rows.Count);
        Assert.Equal("1", rows[0].Cells[0].Text);          // index + 1
        Assert.Equal("Chair", rows[0].Cells[1].Text);      // item.name
        Assert.Contains("20,00", rows[0].Cells[2].Text);   // currency(10 * 2)
        Assert.Equal("2", rows[1].Cells[0].Text);
        Assert.Equal(TextAlign.Right, rows[0].Cells[2].Style.Align);
        Assert.Equal(4f, rows[0].RowStyle.PaddingVertical);
    }

    [Fact]
    public void Table_ComputesFooterAggregate()
    {
        var document = CreateEngine().Build(InvoiceYaml, InvoiceData());

        var table = Assert.IsType<TableNode>(Assert.Single(document.Page.Content));
        Assert.Equal(2, table.Footer.Count);
        Assert.Equal(2, table.Footer[0].Span);
        Assert.Equal("Total", table.Footer[0].Text);
        Assert.Contains("35,00", table.Footer[1].Text);    // sum(price*qty) = 20 + 15
    }

    [Fact]
    public void Table_GroupBy_BuildsSectionsWithSubtotals()
    {
        const string yaml = """
            kind: report
            name: payroll
            content:
              - table:
                  data: data.employees
                  groupBy: department
                  group:
                    header: { text: "{{ group.key }}", style: { bold: true } }
                    footer: { text: "Subtotal: {{ currency(sum(group.items, 'salary')) }}" }
                  columns:
                    - { header: "Name",   cell: "{{ item.name }}" }
                    - { header: "Salary", cell: "{{ currency(item.salary) }}", align: right }
            """;

        var data = Data(new
        {
            employees = new[]
            {
                new { name = "Ana", department = "IT", salary = 100 },
                new { name = "Bia", department = "HR", salary = 200 },
                new { name = "Caio", department = "IT", salary = 300 },
            },
        });

        var document = CreateEngine().Build(yaml, data);
        var table = Assert.IsType<TableNode>(Assert.Single(document.Page.Content));

        Assert.Equal(2, table.Sections.Count);                       // IT, HR (first-seen order)
        Assert.Equal("IT", table.Sections[0].Header!.Text);
        Assert.Equal(2, table.Sections[0].Header!.Span);             // spans both columns
        Assert.Equal(2, table.Sections[0].Rows.Count);              // Ana, Caio
        Assert.Contains("400,00", table.Sections[0].Footer!.Text);  // 100 + 300
        Assert.Equal("HR", table.Sections[1].Header!.Text);
        Assert.Single(table.Sections[1].Rows);
        Assert.Contains("200,00", table.Sections[1].Footer!.Text);
    }

    [Fact]
    public void Table_RendersInvoiceToPdf()
    {
        var bytes = CreateEngine().RenderPdf(InvoiceYaml, InvoiceData());

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }

    [Fact]
    public void Table_RendersGroupedToPdf()
    {
        const string yaml = """
            kind: report
            name: payroll
            content:
              - table:
                  data: data.employees
                  groupBy: department
                  group:
                    header: { text: "{{ group.key }}", style: { bold: true, background: "#EEEEEE" } }
                    footer: { text: "Subtotal: {{ currency(sum(group.items, 'salary')) }}", align: right }
                  columns:
                    - { width: 3*, header: "Name",   cell: "{{ item.name }}" }
                    - { width: 1*, header: "Salary", cell: "{{ currency(item.salary) }}", align: right }
            """;

        var data = Data(new
        {
            employees = new[]
            {
                new { name = "Ana", department = "IT", salary = 100 },
                new { name = "Caio", department = "IT", salary = 300 },
                new { name = "Bia", department = "HR", salary = 200 },
            },
        });

        var bytes = CreateEngine().RenderPdf(yaml, data);

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }
}
