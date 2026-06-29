namespace Buelo.Contracts;

/// <summary>An operational record of a render (append-heavy; lives in the relational store, §13).</summary>
public class RenderEvent
{
    public int Id { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public string Engine { get; set; } = "declarative";   // declarative | csharp
    public string Format { get; set; } = "pdf";
    public int ByteCount { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Records and queries render history. Backed by EF Core in production; no-op by default.</summary>
public interface IRenderLog
{
    Task LogAsync(RenderEvent renderEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RenderEvent>> RecentAsync(int count = 50, CancellationToken cancellationToken = default);
}
