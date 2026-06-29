# PageSettings — Parameterizable Page Configuration

## Overview

The `PageSettings` system lets you dynamically configure all the visual aspects of a PDF page directly in your report, without having to hardcode values inside the template.

## What is Configurable

The `PageSettings` class provides control over:

- **Page size**: A4, Letter, Legal, A3, A5
- **Margins**: Horizontal and vertical (in centimeters)
- **Colors**: Background color and default text color
- **Watermark**: Text, color, opacity, and font size
- **Headers/Footers**: Enable or disable
- **Default font**: Font size for body text

## Architecture

### Data Flow

```
ReportRequest/TemplateRenderRequest
    ↓
    PageSettings (optional)
    ↓
ReportController
    ↓
TemplateEngine.RenderAsync(template, data, mode, pageSettings)
    ↓
ReportContext
    ├─ ctx.Data
    ├─ ctx.Helpers
    ├─ ctx.Globals
    └─ ctx.PageSettings ← HERE!
    ↓
IReport.GenerateReport(ctx)
    ↓
PDF with the settings applied
```

### Precedence

1. **Request PageSettings** (if provided) — overrides everything
2. **TemplateRecord.PageSettings** — default for rendering
3. **PageSettings.Default()** — global fallback (A4 with 2cm margins)

## Usage Examples

### 1. Use Pre-configured Defaults

```csharp
// Render with factory defaults
var settings = PageSettings.Letter();           // Letter with 1" margins
var settings = PageSettings.A4Compact();        // A4 with 1cm margins
var settings = PageSettings.WithWatermark("DRAFT");  // With watermark
```

### 2. Template Builder with PageSettings

```csharp
// Template Builder mode — access the settings via ctx
const string template = @"
Document.Create(c => 
{
    c.Page(p => 
    {
        var settings = ctx.PageSettings;
        
        p.Size(GetPageSize(settings.PageSize));
        p.Margin(settings.MarginVertical, settings.MarginHorizontal, Unit.Centimetre);
        
        p.Header()
            .Text((string)data.name)
            .FontSize(36)
            .FontColor(Colors.Blue.Medium);
            
        p.Content()
            .Column(x => 
            {
                x.Item().Text(""Content here"");
            });
            
        p.Footer()
            .AlignCenter()
            .Text(x => 
            {
                x.Span(""Page "");
                x.CurrentPageNumber();
            });
    });
}).GeneratePdf()
";

// Send via API
var request = new ReportRequest
{
    Template = template,
    FileName = "my-report.pdf",
    Data = new { name = "Important Report" },
    PageSettings = new PageSettings
    {
        PageSize = "A4",
        MarginHorizontal = 2.0f,
        MarginVertical = 2.0f,
        BackgroundColor = "#F5F5F5",
        WatermarkText = "CONFIDENTIAL",
        WatermarkOpacity = 0.2f
    }
};

var response = await client.PostAsJsonAsync("/api/report/render", request);
```

### 2.1 Sections Mode with fallback to PageSettings

In `Sections` mode, if the `page => { ... }` block is omitted, the engine automatically
applies `ctx.PageSettings` (size, margins, and default font). You only
declare header/body/footer fluently.

```csharp
const string sectionsTemplate = @"
@import header from ""company-header""

page.Content()
    .PaddingVertical(1, Unit.Centimetre)
    .Column(x =>
    {
        x.Spacing(8);
        x.Item().Text((string)data.name);
        x.Item().Text(""Report generated in Sections mode"");
    });

page.Footer()
    .AlignCenter()
    .Text(x => { x.Span(""Page ""); x.CurrentPageNumber(); });
";

var request = new ReportRequest
{
    Template = sectionsTemplate,
    Mode = TemplateMode.Sections,
    Data = new { name = "Commercial Report" },
    PageSettings = new PageSettings
    {
        PageSize = "Letter",
        MarginHorizontal = 1.5f,
        MarginVertical = 2.0f,
        DefaultFontSize = 11
    }
};
```

If you want to visually override the page settings inside the template
itself, include the `page => { ... }` block explicitly.

### 3. FullClass Template with PageSettings

```csharp
public class Report : IReport
{
    public byte[] GenerateReport(ReportContext ctx)
    {
        var data = ctx.Data;
        var settings = ctx.PageSettings;
        
        return Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(GetPageSize(settings.PageSize));
                p.Margin(settings.MarginVertical, settings.MarginHorizontal, Unit.Centimetre);
                
                // Apply watermark if configured
                if (!string.IsNullOrEmpty(settings.WatermarkText))
                {
                    p.Background()
                        .AlignCenter()
                        .AlignMiddle()
                        .Text(settings.WatermarkText)
                        .FontSize(settings.WatermarkFontSize)
                        .Opacity(settings.WatermarkOpacity);
                }
                
                p.Header()
                    .Text((string)data.name)
                    .SemiBold()
                    .FontSize(36);
                    
                p.Content()
                    .Text((string)data.description);
            });
        }).GeneratePdf();
    }
    
    private static PageSize GetPageSize(string size) => size.ToUpper() switch
    {
        "LETTER" => PageSizes.Letter,
        "LEGAL" => PageSizes.Legal,
        "A3" => PageSizes.A3,
        "A4" => PageSizes.A4,
        "A5" => PageSizes.A5,
        _ => PageSizes.A4
    };
}
```

### 4. Save Template with Settings

```csharp
var template = new TemplateRecord
{
    Name = "Sales Report",
    Description = "Monthly report with watermark",
    Template = @"Document.Create(...).GeneratePdf()",
    Mode = TemplateMode.Builder,
    MockData = new { /* ... */ },
    DefaultFileName = "sales.pdf",
    
    // Settings that will be the default
    PageSettings = new PageSettings
    {
        PageSize = "A4",
        MarginHorizontal = 2.0f,
        MarginVertical = 2.5f,
        BackgroundColor = "#FFFFFF",
        WatermarkText = "INTERNAL COPY",
        WatermarkColor = "#DEDEDE",
        WatermarkOpacity = 0.15f,
        WatermarkFontSize = 50,
        DefaultFontSize = 11,
        DefaultTextColor = "#333333",
        ShowHeader = true,
        ShowFooter = true
    }
};

await store.SaveAsync(template);
```

### 5. Render with Settings Override

```csharp
// GET /api/report/render/{templateId}
// Body (optional):
var overrides = new TemplateRenderRequest
{
    Data = new { /* data */ },
    FileName = "special.pdf",
    
    // Overrides the template's settings
    PageSettings = new PageSettings
    {
        PageSize = "Letter",
        WatermarkText = "DRAFT - " + DateTime.Now.ToString("yyyy-MM-dd")
    }
};

var response = await client.PostAsJsonAsync($"/api/report/render/{templateId}", overrides);
```

## PageSettings Properties

```csharp
public class PageSettings
{
    // Page size (e.g.: "A4", "Letter", "Legal")
    public string PageSize { get; set; } = "A4";

    // Margins in centimeters
    public float MarginHorizontal { get; set; } = 2.0f;
    public float MarginVertical { get; set; } = 2.0f;

    // Colors (hex format)
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string DefaultTextColor { get; set; } = "#000000";

    // Watermark
    public string? WatermarkText { get; set; }
    public string WatermarkColor { get; set; } = "#CCCCCC";
    public float WatermarkOpacity { get; set; } = 0.3f;
    public int WatermarkFontSize { get; set; } = 60;

    // Typography
    public int DefaultFontSize { get; set; } = 12;

    // Layout
    public bool ShowHeader { get; set; } = true;
    public bool ShowFooter { get; set; } = true;
}
```

## Helper Methods (Factory Methods)

```csharp
// A4 default with 2cm margins
var settings = PageSettings.Default();

// Letter with 1-inch margins (2.54cm)
var settings = PageSettings.Letter();

// Compact A4 with 1cm margins
var settings = PageSettings.A4Compact();

// With a predefined watermark
var settings = PageSettings.WithWatermark("CONFIDENTIAL");
```

## Precedence Flow

When rendering a template:

1. If `PageSettings` is provided in the request → use it
2. Otherwise, if the template has `PageSettings` configured → use it
3. Otherwise → use `PageSettings.Default()`

## Backward Compatibility

- Existing templates keep working without changes
- `PageSettings` is optional in every request
- If not provided, it uses sensible defaults (A4, 2cm margins)

## Best Practices

1. **For highly customizable reports**: Use `Builder` templates and access `ctx.PageSettings`
2. **For standard reports**: Configure `PageSettings` on the `TemplateRecord` and reuse it
3. **For dynamic overrides**: Provide `PageSettings` in the request when needed
4. **For watermarks**: Use `PageSettings.WithWatermark()` or configure it manually
5. **For multiple variations**: Create multiple `TemplateRecord`s with different settings

## Configuration Examples by Scenario

### Formal Report
```csharp
PageSettings.Default()
```

### Draft Report
```csharp
PageSettings.WithWatermark("DRAFT")
```

### Shipping Label
```csharp
new PageSettings 
{ 
    PageSize = "A4", 
    MarginHorizontal = 0.5f, 
    MarginVertical = 0.5f 
}
```

### Confidential Document
```csharp
new PageSettings
{
    PageSize = "A4",
    WatermarkText = "CONFIDENTIAL",
    WatermarkOpacity = 0.1f,
    BackgroundColor = "#FFF8DC"
}
```
