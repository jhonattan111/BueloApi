# 🎨 PageSettings — Parameterizable Page Configuration

## ✅ What was implemented

I refactored the Buelo system to allow **fully parameterizable page settings** through a new `PageSettings` class. You can now configure page size, margins, colors, watermark, and much more **directly in the report**, without having to hardcode anything in the template.

---

## 📋 Summary of Changes

### 1. **New Class: `PageSettings`** (Buelo.Contracts)
```csharp
public class PageSettings
{
    public string PageSize { get; set; } = "A4";                    // A4, Letter, Legal, etc
    public float MarginHorizontal { get; set; } = 2.0f;             // in centimeters
    public float MarginVertical { get; set; } = 2.0f;               // in centimeters
    public string BackgroundColor { get; set; } = "#FFFFFF";        // hex
    public string DefaultTextColor { get; set; } = "#000000";       // hex
    
    // Watermark
    public string? WatermarkText { get; set; }
    public string WatermarkColor { get; set; } = "#CCCCCC";
    public float WatermarkOpacity { get; set; } = 0.3f;
    public int WatermarkFontSize { get; set; } = 60;
    
    // Typography and Layout
    public int DefaultFontSize { get; set; } = 12;
    public bool ShowHeader { get; set; } = true;
    public bool ShowFooter { get; set; } = true;
}
```

### 2. **`ReportContext` Extended**
Now includes the `PageSettings` property:
```csharp
public class ReportContext
{
    public dynamic Data { get; set; }
    public IHelperRegistry Helpers { get; set; }
    public IDictionary<string, object>? Globals { get; set; }
    public PageSettings PageSettings { get; set; } = PageSettings.Default();  // ← NEW
}
```

### 3. **`TemplateRecord` Extended**
Now allows persisting the settings with the template:
```csharp
public class TemplateRecord
{
    // ... existing properties ...
    public PageSettings PageSettings { get; set; } = PageSettings.Default();  // ← NEW
}
```

### 4. **`ReportRequest` and `TemplateRenderRequest` Extended**
They allow passing settings in the request:
```csharp
public class ReportRequest
{
    public string Template { get; set; }
    public string FileName { get; set; } = "report.pdf";
    public object Data { get; set; }
    public TemplateMode Mode { get; set; } = TemplateMode.FullClass;
    public PageSettings? PageSettings { get; set; }  // ← NEW (optional)
}

public class TemplateRenderRequest
{
    public object? Data { get; set; }
    public string? FileName { get; set; }
    public PageSettings? PageSettings { get; set; }  // ← NEW (optional)
}
```

### 5. **`TemplateEngine` Updated**
The methods now accept `PageSettings`:
```csharp
public async Task<byte[]> RenderAsync(
    string template, 
    object data, 
    TemplateMode mode = TemplateMode.FullClass, 
    PageSettings? pageSettings = null  // ← NEW
)

public Task<byte[]> RenderTemplateAsync(
    TemplateRecord template, 
    object data, 
    PageSettings? pageSettings = null  // ← NEW
)
```

### 6. **`ReportController` Updated**
Passes `PageSettings` through the rendering pipeline.

---

## 🚀 How to Use

### Option 1: Template Builder with Settings

```csharp
const string template = @"
Document.Create(container => { 
    container.Page(page => { 
        var settings = ctx.PageSettings;
        page.Size(GetPageSize(settings.PageSize));
        page.Margin(settings.MarginVertical, settings.MarginHorizontal, Unit.Centimetre); 
        page.PageColor(ParseColor(settings.BackgroundColor));
        page.DefaultTextStyle(x => x.FontSize(settings.DefaultFontSize));
        
        page.Header().Text((string)data.name).SemiBold().FontSize(36).FontColor(Colors.Blue.Medium);
        page.Content().Column(x => { 
            x.Item().Text(Placeholders.LoremIpsum()); 
            x.Item().Image(Placeholders.Image(200, 100)); 
        });
        
        if (!string.IsNullOrEmpty(settings.WatermarkText))
        {
            page.Background()
                .AlignCenter().AlignMiddle()
                .Text(settings.WatermarkText)
                .FontSize(settings.WatermarkFontSize)
                .Opacity(settings.WatermarkOpacity);
        }
        
        page.Footer().AlignCenter().Text(x => { 
            x.Span(""Page ""); 
            x.CurrentPageNumber(); 
        }); 
    }); 
}).GeneratePdf()
";

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
        DefaultFontSize = 20,
        WatermarkText = "CONFIDENTIAL"
    }
};
```

### Option 2: Use Pre-configured Presets

```csharp
// A4 default with 2cm margins
var settings = PageSettings.Default();

// Letter with 1" margins
var settings = PageSettings.Letter();

// Compact A4 with 1cm margins
var settings = PageSettings.A4Compact();

// With watermark
var settings = PageSettings.WithWatermark("DRAFT");
```

### Option 3: Save Template with Settings

```csharp
var template = new TemplateRecord
{
    Name = "Sales Report",
    Template = /* your template here */,
    Mode = TemplateMode.Builder,
    PageSettings = new PageSettings
    {
        PageSize = "A4",
        MarginHorizontal = 2.0f,
        MarginVertical = 2.5f,
        WatermarkText = "CONFIDENTIAL"
    }
};

await store.SaveAsync(template);
```

---

## 🔄 Settings Precedence

When rendering, settings are applied in this order:

1. **PageSettings provided in the request** (highest priority)
2. **PageSettings from the TemplateRecord** (if the template is saved in the database)
3. **PageSettings.Default()** (fallback)

```
Request PageSettings?  → Use it
                ↓ no
Template PageSettings? → Use it
                ↓ no
PageSettings.Default() → Use it (A4, 2cm margins)
```

---

## 📁 Files Created/Modified

### Created:
- `Buelo.Contracts/PageSettings.cs` — New class with all the settings
- `PAGE_SETTINGS_GUIDE.md` — Complete documentation
- `Buelo.Tests/Engine/PageSettingsExamples.cs` — Example templates
- `Buelo.Tests/Engine/PageSettingsEngineTests.cs` — Unit tests

### Modified:
- `ReportContext.cs` — Added `PageSettings`
- `TemplateRecord.cs` — Added `PageSettings`
- `ReportRequest.cs` — Added `PageSettings?`
- `TemplateRenderRequest.cs` — Added `PageSettings?`
- `TemplateEngine.cs` — Updated to accept `PageSettings`
- `ReportController.cs` — Passes `PageSettings` in the pipeline
- `ReportControllerTests.cs` — Added `PageSettings` tests

---

## ✅ Tests

✅ **28 tests passing** — Including:
- Preset tests (`Default()`, `Letter()`, `A4Compact()`, `WithWatermark()`)
- Rendering tests with custom settings
- Settings override tests in the request
- Fallback tests for `TemplateRecord.PageSettings`

---

## 🎯 Usage Examples By Scenario

### Formal Report
```csharp
PageSettings.Default()
```

### Draft with Watermark
```csharp
PageSettings.WithWatermark("DRAFT")
```

### Compact Label
```csharp
new PageSettings 
{ 
    PageSize = "A4", 
    MarginHorizontal = 0.5f, 
    MarginVertical = 0.5f,
    ShowHeader = false,
    ShowFooter = false
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

---

## 🔄 Compatibility

✅ **Fully compatible with existing code** — `PageSettings` is optional in every request. If not provided, it uses sensible defaults.

---

## 📝 Next Steps (Optional)

1. Store `PageSettings` in a database (if switching to persistence)
2. UI to edit `PageSettings` visually
3. Support for different custom paper sizes
4. Saved themes/presets
5. Template version history with different settings

---

## 🎉 Final Result

You can now:
- ✅ Pass the entire Document.Create() declaration as a Builder template
- ✅ Configure size, margins, color, watermark directly in the report
- ✅ Parameterize all page settings via `ctx.PageSettings`
- ✅ Reuse templates with different settings
- ✅ Use pre-configured presets for common scenarios
