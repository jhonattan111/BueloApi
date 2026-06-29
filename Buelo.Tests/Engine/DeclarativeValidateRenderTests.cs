using System.Text.Json;
using Buelo.Engine.Declarative;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class DeclarativeValidateRenderTests
{
    public DeclarativeValidateRenderTests()
    {
        Settings.License = LicenseType.Community;
    }

    private static DeclarativeReportEngine CreateEngine() => new(new DeclarativeInterpreter());

    private static JsonElement Data(object value)
        => JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));

    private const string CpfModule = """
        kind: validator
        name: cpf
        format: "###.###.###-##"
        rules:
          - { digits: 11 }
          - { checksum: { scheme: mod11, weights: [10, 9, 8, 7, 6, 5, 4, 3, 2] } }
        """;

    private const string Report = """
        kind: report
        name: r
        validate:
          "data.cpf": cpf
        content:
          - text: { value: "CPF: {{ data.cpf }}" }
        """;

    [Fact]
    public void Valid_data_passes_the_gate_and_renders()
    {
        var bytes = CreateEngine().RenderPdf(Report, Data(new { cpf = "529.982.247-25" }), [CpfModule]);
        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }

    [Fact]
    public void Invalid_data_fails_the_render()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => CreateEngine().RenderPdf(Report, Data(new { cpf = "123" }), [CpfModule]));
        Assert.Contains("Validation failed", ex.Message);
    }
}
