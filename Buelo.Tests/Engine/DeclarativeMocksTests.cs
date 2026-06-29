using System.Text.Json;
using Buelo.Engine;
using Buelo.Engine.Declarative;
using QuestPDF;
using QuestPDF.Infrastructure;

namespace Buelo.Tests.Engine;

/// <summary>
/// Verifies the example declarative definitions shipped in <c>Buelo.Api/definitions</c> are valid
/// and render end-to-end (parse + resolve imports + lower + QuestPDF). Guards the mocks against rot.
/// </summary>
public class DeclarativeMocksTests
{
    public DeclarativeMocksTests()
    {
        Settings.License = LicenseType.Community;
    }

    private static string DefinitionsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Buelo.Api", "definitions");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate Buelo.Api/definitions from the test directory.");
    }

    [Theory]
    [InlineData("hello", "hello.json")]
    [InlineData("invoice", "invoice.json")]
    [InlineData("employees", "employees.json")]
    public async Task Mock_report_renders_to_pdf(string report, string dataFile)
    {
        var root = DefinitionsRoot();
        var store = new FileSystemDefinitionStore(root);
        var json = await File.ReadAllTextAsync(Path.Combine(root, "data", dataFile));
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        var engine = new DeclarativeReportEngine(new DeclarativeInterpreter());
        var bytes = await engine.RenderStoredAsync(report, data, store);

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF"u8.ToArray(), bytes.AsSpan(0, 4).ToArray());
    }
}
