using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Buelo.Engine.Declarative;

/// <summary>
/// Parses a YAML report definition into a <see cref="ReportDefinition"/> AST.
/// YAML is the authoring format for definitions; the data that feeds a report stays JSON
/// (decision 2026-06-28). Unknown properties are ignored so newer block kinds in a file
/// don't break an older engine.
/// </summary>
public static class DeclarativeParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static ReportDefinition Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            throw new InvalidOperationException("Report definition is empty.");

        try
        {
            return Deserializer.Deserialize<ReportDefinition>(yaml)
                ?? throw new InvalidOperationException("Report definition could not be parsed.");
        }
        catch (YamlException ex)
        {
            // Surface a clean, user-facing message with the location of the YAML error.
            throw new InvalidOperationException(
                $"Invalid YAML at line {ex.Start.Line}, column {ex.Start.Column}: {ex.Message}", ex);
        }
    }
}
