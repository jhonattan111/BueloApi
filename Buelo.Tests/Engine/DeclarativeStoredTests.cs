using System.Text.Json;
using Buelo.Engine;
using Buelo.Engine.Declarative;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

public class DeclarativeStoredTests
{
    public DeclarativeStoredTests()
    {
        Settings.License = LicenseType.Community;
    }

    private static DeclarativeReportEngine CreateEngine() => new(new DeclarativeInterpreter());

    private static JsonElement Data(object value)
        => JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));

    private const string ComponentYaml = """
        kind: component
        name: layout
        params: { title: { type: string } }
        slots: [content]
        body:
          - text: { value: "{{ title }}", class: title }
          - slot: content
        """;

    private const string StylesYaml = """
        kind: styles
        name: corp
        classes:
          title: { size: 16, bold: true }
        """;

    private const string ReportYaml = """
        kind: report
        name: invoice
        import:
          - styles: corp
          - component: layout
        use: layout
        with: { title: "Invoice" }
        content:
          - text: { value: "body", class: title }
        """;

    private static async Task<InMemoryDefinitionStore> SeedStore()
    {
        var store = new InMemoryDefinitionStore();
        await store.SaveAsync("component", "layout", ComponentYaml);
        await store.SaveAsync("styles", "corp", StylesYaml);
        await store.SaveAsync("report", "invoice", ReportYaml);
        return store;
    }

    [Fact]
    public async Task LoadProject_collects_imported_modules()
    {
        var store = await SeedStore();

        var (definition, modules) = await CreateEngine().LoadProjectAsync("invoice", store);

        Assert.Equal("invoice", definition.Name);
        Assert.Equal(2, modules.Count); // styles + component
    }

    [Fact]
    public async Task RenderStored_resolves_imports_and_renders_pdf()
    {
        var store = await SeedStore();

        var bytes = await CreateEngine().RenderStoredAsync("invoice", Data(new { }), store);

        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }

    [Fact]
    public async Task RenderStored_missing_report_throws()
    {
        var store = new InMemoryDefinitionStore();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateEngine().RenderStoredAsync("nonexistent", null, store));
    }

    [Fact]
    public async Task RenderStored_missing_import_throws()
    {
        var store = new InMemoryDefinitionStore();
        await store.SaveAsync("report", "r", "kind: report\nname: r\nimport:\n  - styles: missing\ncontent: []");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateEngine().RenderStoredAsync("r", null, store));
        Assert.Contains("missing", ex.Message);
    }
}
