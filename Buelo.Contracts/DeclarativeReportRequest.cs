namespace Buelo.Contracts;

/// <summary>
/// Request to render a report from a declarative YAML definition (engine: declarative).
/// The definition is YAML (authoring format); the data stays JSON (comes from the API).
/// </summary>
public class DeclarativeReportRequest
{
    /// <summary>The YAML report definition (<c>kind: report</c>).</summary>
    public string Definition { get; set; } = string.Empty;

    /// <summary>Data bound to the report, as JSON.</summary>
    public object? Data { get; set; }

    /// <summary>Imported module definitions (styles/component/theme/formats/lib) as YAML documents.</summary>
    public List<string>? Modules { get; set; }

    /// <summary>Output file name for the rendered report.</summary>
    public string FileName { get; set; } = "report.pdf";
}
