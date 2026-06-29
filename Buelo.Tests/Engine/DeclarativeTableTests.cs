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
        name: fatura
        content:
          - table:
              data: data.itens
              rowStyle: { paddingY: 4, borderBottom: "1px #DDD" }
              columns:
                - { width: 25px, header: "#",        cell: "{{ index + 1 }}" }
                - { width: 3*,   header: "Produto",  cell: "{{ item.nome }}" }
                - { width: 1*,   header: "Total",    cell: "{{ moeda(item.preco * item.qtd) }}", align: right }
              footer:
                - { span: 2, text: "Total", style: { bold: true, align: right } }
                - { text: "{{ moeda(sum(data.itens, 'preco * qtd')) }}", align: right }
        """;

    private static JsonElement InvoiceData() => Data(new
    {
        itens = new[]
        {
            new { nome = "Cadeira", preco = 10, qtd = 2 },
            new { nome = "Mesa", preco = 5, qtd = 3 },
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
        Assert.Equal("Cadeira", rows[0].Cells[1].Text);    // item.nome
        Assert.Contains("20,00", rows[0].Cells[2].Text);   // moeda(10 * 2)
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
        Assert.Contains("35,00", table.Footer[1].Text);    // sum(preco*qtd) = 20 + 15
    }

    [Fact]
    public void Table_GroupBy_BuildsSectionsWithSubtotals()
    {
        const string yaml = """
            kind: report
            name: folha
            content:
              - table:
                  data: data.colaboradores
                  groupBy: departamento
                  group:
                    header: { text: "{{ group.key }}", style: { bold: true } }
                    footer: { text: "Subtotal: {{ moeda(sum(group.items, 'salario')) }}" }
                  columns:
                    - { header: "Nome",    cell: "{{ item.nome }}" }
                    - { header: "Salário", cell: "{{ moeda(item.salario) }}", align: right }
            """;

        var data = Data(new
        {
            colaboradores = new[]
            {
                new { nome = "Ana", departamento = "TI", salario = 100 },
                new { nome = "Bia", departamento = "RH", salario = 200 },
                new { nome = "Caio", departamento = "TI", salario = 300 },
            },
        });

        var document = CreateEngine().Build(yaml, data);
        var table = Assert.IsType<TableNode>(Assert.Single(document.Page.Content));

        Assert.Equal(2, table.Sections.Count);                       // TI, RH (first-seen order)
        Assert.Equal("TI", table.Sections[0].Header!.Text);
        Assert.Equal(2, table.Sections[0].Header!.Span);             // spans both columns
        Assert.Equal(2, table.Sections[0].Rows.Count);              // Ana, Caio
        Assert.Contains("400,00", table.Sections[0].Footer!.Text);  // 100 + 300
        Assert.Equal("RH", table.Sections[1].Header!.Text);
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
            name: folha
            content:
              - table:
                  data: data.colaboradores
                  groupBy: departamento
                  group:
                    header: { text: "{{ group.key }}", style: { bold: true, background: "#EEEEEE" } }
                    footer: { text: "Subtotal: {{ moeda(sum(group.items, 'salario')) }}", align: right }
                  columns:
                    - { width: 3*, header: "Nome",    cell: "{{ item.nome }}" }
                    - { width: 1*, header: "Salário", cell: "{{ moeda(item.salario) }}", align: right }
            """;

        var data = Data(new
        {
            colaboradores = new[]
            {
                new { nome = "Ana", departamento = "TI", salario = 100 },
                new { nome = "Caio", departamento = "TI", salario = 300 },
                new { nome = "Bia", departamento = "RH", salario = 200 },
            },
        });

        var bytes = CreateEngine().RenderPdf(yaml, data);

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }
}
