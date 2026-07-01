# `PageSettings` — page configuration for the C# path

`PageSettings` (`Buelo.Contracts/PageSettings.cs`) is a request-time knob for the **C# `IDocument`**
path — page size, margins, colors, watermark, default font, header/footer visibility. It is separate
from the declarative YAML path's much simpler `meta.page` (see [`report.md`](report.md)).

## Properties

```csharp
public class PageSettings
{
    public string PageSize { get; set; } = "A4";              // A4, Letter, Legal, A3, A5
    public float MarginHorizontal { get; set; } = 2.0f;        // cm
    public float MarginVertical { get; set; } = 2.0f;          // cm
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string? WatermarkText { get; set; }                 // null/empty = no watermark
    public string WatermarkColor { get; set; } = "#CCCCCC";
    public float WatermarkOpacity { get; set; } = 0.3f;
    public int WatermarkFontSize { get; set; } = 60;
    public int DefaultFontSize { get; set; } = 12;
    public string DefaultTextColor { get; set; } = "#000000";
    public bool ShowHeader { get; set; } = true;
    public bool ShowFooter { get; set; } = true;
    public string? DataSourcePath { get; set; }                // editor-only: workspace-relative mock data path
}
```

Factory methods: `PageSettings.Default()` (A4, 2cm), `PageSettings.Letter()` (1" margins),
`PageSettings.A4Compact()` (1cm margins), `PageSettings.WithWatermark(text)`.

`DataSourcePath` is an editor concern (stored per-file in the frontend's `buelo.reportSettings`
localStorage key) — it rides along in `PageSettings` but the render pipeline itself doesn't read it.

## How a template opts in

`PageSettings` only reaches your template if its `IDocument`-implementing class declares a
constructor parameter of that type — the engine's `CreateDocumentInstance` (`Buelo.Engine/TemplateEngine.cs`)
picks the constructor with the most parameters and, for any parameter of type `PageSettings`, passes
the effective settings (or `PageSettings.Default()` if none were supplied); every other parameter gets
the data. If your class only takes `dynamic data`, `PageSettings` is resolved but silently unused —
nothing applies it to `page.Size`/`page.Margin` automatically. You read it yourself:

```csharp
public class InvoiceDocument : IDocument
{
    private readonly dynamic _data;
    private readonly PageSettings _settings;

    public InvoiceDocument(dynamic data, PageSettings settings)
    {
        _data = data;
        _settings = settings;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        container.Page(page =>
        {
            page.Size(ToPageSize(_settings.PageSize));
            page.Margin(_settings.MarginVertical, _settings.MarginHorizontal, Unit.Centimetre);
            if (!string.IsNullOrEmpty(_settings.WatermarkText))
                page.Background().AlignCenter().AlignMiddle()
                    .Text(_settings.WatermarkText).FontSize(_settings.WatermarkFontSize)
                    .Opacity(_settings.WatermarkOpacity);
            page.Content().Text((string)_data.title);
        });

    private static PageSize ToPageSize(string size) => size.ToUpperInvariant() switch
    {
        "LETTER" => PageSizes.Letter,
        "LEGAL" => PageSizes.Legal,
        "A3" => PageSizes.A3,
        "A5" => PageSizes.A5,
        _ => PageSizes.A4,
    };
}
```

## Precedence

`TemplateEngine.MergeSettings(template, request)` = **request `??` template `??` `Default()`**:

1. `PageSettings` on the inbound request (`ReportRequest.PageSettings` for ad-hoc renders,
   `TemplateRenderRequest.PageSettings` when rendering a stored template by id) — wins if present.
2. Otherwise the stored `TemplateRecord.PageSettings`.
3. Otherwise `PageSettings.Default()`.

```csharp
var request = new ReportRequest
{
    Template = source,
    Data = new { title = "Q1 Report" },
    PageSettings = new PageSettings { PageSize = "A4", WatermarkText = "DRAFT", WatermarkOpacity = 0.2f },
};
// POST /api/report/render
```

Overriding a stored template at render time works the same way through
`POST /api/report/render/{templateId}` with a `TemplateRenderRequest` body.
