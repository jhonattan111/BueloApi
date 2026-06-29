namespace Buelo.Contracts;

/// <summary>Request to render a stored declarative report (resolved by name, with its imports).</summary>
public class StoredReportRequest
{
    /// <summary>Data bound to the report, as JSON.</summary>
    public object? Data { get; set; }

    /// <summary>Optional output file name (defaults to the report name).</summary>
    public string? FileName { get; set; }
}
