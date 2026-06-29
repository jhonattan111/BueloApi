using System.Text.Json;
using Buelo.Engine.Declarative.Schema;

namespace Buelo.Tests.Engine;

public class DeclarativeSchemaTests
{
    [Fact]
    public void Generates_schema_for_every_kind()
    {
        Assert.NotEmpty(DeclarativeSchemas.Kinds);
        foreach (var kind in DeclarativeSchemas.Kinds)
        {
            var json = JsonSerializer.Serialize(DeclarativeSchemas.Generate(kind));
            Assert.Contains("$schema", json);
            Assert.Contains("definitions", json);
        }
    }

    [Fact]
    public void Report_schema_describes_content_array_and_handles_recursion()
    {
        var json = JsonSerializer.Serialize(DeclarativeSchemas.Generate("report"));
        using var document = JsonDocument.Parse(json);

        var definitions = document.RootElement.GetProperty("definitions");
        var content = definitions.GetProperty("ReportDefinition").GetProperty("properties").GetProperty("content");
        Assert.Equal("array", content.GetProperty("type").GetString());

        // Recursive block type is referenced via $ref, not inlined forever.
        Assert.True(definitions.TryGetProperty("ContentBlock", out _));
    }
}
