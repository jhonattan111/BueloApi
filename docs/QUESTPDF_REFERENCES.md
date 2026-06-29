# 📚 QuestPDF Template References

This document catalogs the reference QuestPDF templates provided to help develop the new Buelo templates.

## 📋 Available Templates

### 1. **InvoiceDocument.cs** - Invoice Template
**Location**: Attachments (QuestPDFShowcase project)

**Features**:
- Clean, professional layout
- Seller and buyer data
- Items table with quantity/price
- Summary of subtotal, taxes, total
- Header and footer with information

**Data Model**:
```csharp
public class InvoiceModel
{
    public int InvoiceNumber { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public string SellerName { get; set; }
    public string SellerAddress { get; set; }
    public string SellerEmail { get; set; }
    public string BuyerName { get; set; }
    public string BuyerAddress { get; set; }
    public string BuyerEmail { get; set; }
    public List<InvoiceItem> Items { get; set; }
    public decimal TaxRate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal GrandTotal { get; set; }
}
```

**Use in Buelo**:
```csharp
public class InvoiceDocument : IDocument
{
    private readonly dynamic _data;
    
    public InvoiceDocument(dynamic data) => _data = data;
    
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            // ... implement layout based on the reference
        });
    }
}
```

---

### 2. **FinancialDashboardDocument.cs** - Financial Dashboard
**Features**:
- Monthly statistics (Revenue, Expenses, Profit)
- KPI cards with highlighted colors
- Financial data table
- Department chart with bars
- Landscape layout with multiple elements

**Data Model**:
```csharp
public class MonthlyStat
{
    public string Month { get; set; }
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal Profit { get; set; }
}

public class DepartmentBudget
{
    public string Department { get; set; }
    public decimal Allocated { get; set; }
    public decimal Spent { get; set; }
    public double UsagePercent { get; set; }
}

public class FinancialDashboardModel
{
    public int Year { get; set; }
    public string CompanyName { get; set; }
    public List<MonthlyStat> MonthlyStats { get; set; }
    public List<DepartmentBudget> Departments { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
}
```

**Useful Techniques**:
- Cards with highlight colors (KPI)
- Tables with multiple columns
- Vertical bar charts
- Row layout for side-by-side data

---

### 3. **ProductCatalogDocument.cs** - Product Catalog
**Features**:
- Custom cover with branding
- Pages by category
- Product grid with images/descriptions
- Badges for "New" and "Featured"
- Star rating
- Stock and price info

**Data Model**:
```csharp
public class Product
{
    public string Name { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
    public string SKU { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public double Rating { get; set; }
    public bool IsNew { get; set; }
    public bool IsFeatured { get; set; }
}

public class ProductCatalogModel
{
    public string StoreName { get; set; }
    public string Tagline { get; set; }
    public string Season { get; set; }
    public List<Product> Products { get; set; }
}
```

**Useful Techniques**:
- Multiple pages (cover + content pages)
- Group by category
- Badges and indicators
- Star ratings
- Grid layout
- Borders and spacing

---

### 4. **SalesPerformanceDocument.cs** - Sales Performance Report
**Features**:
- KPI cards with key metrics
- Bookings vs renewals chart
- Pipeline-by-stage chart
- Quarterly breakdown table
- Multiple pages (overview + details)

**Data Model**:
```csharp
public class SalesMonthlyMetric
{
    public string Month { get; set; }
    public decimal Bookings { get; set; }
    public decimal Renewals { get; set; }
}

public class PipelineStage
{
    public string Name { get; set; }
    public decimal Value { get; set; }
}

public class SalesPerformanceModel
{
    public string CompanyName { get; set; }
    public string QuarterLabel { get; set; }
    public string SalesLead { get; set; }
    public List<SalesMonthlyMetric> MonthlyMetrics { get; set; }
    public List<PipelineStage> PipelineStages { get; set; }
}
```

**Useful Techniques**:
- Multiple pages
- Charts (bar, line, pie)
- KPI cards with colors
- Summary tables
- Page headers with metadata

---

### 5. **OperationsSnapshotDocument.cs** - Operations Snapshot
**Features**:
- Ticket and incident metrics
- KPI cards with colors
- Chart of tickets opened per day
- Team capacity chart
- Compact layout

**Data Model**:
```csharp
public class TeamCapacity
{
    public string TeamName { get; set; }
    public decimal CapacityPercent { get; set; }
}

public class DailyTicketMetric
{
    public string DayLabel { get; set; }
    public decimal Opened { get; set; }
}

public class OperationsSnapshotModel
{
    public string CompanyName { get; set; }
    public string PeriodLabel { get; set; }
    public int ResolvedTickets { get; set; }
    public int OpenIncidents { get; set; }
    public decimal SlaCompliancePercent { get; set; }
    public List<DailyTicketMetric> DailyTickets { get; set; }
    public List<TeamCapacity> TeamCapacity { get; set; }
}
```

---

## 🛠️ Shared Components

### SharedReportLayout.cs
Shared components for layout:
- `ComposeHeader()` - Standardized header with title/subtitle
- `ComposeFooter()` - Footer with page numbering

### SharedChartComponents.cs
Reusable chart components:
- `VerticalBarChart()` - Vertical bar chart
- `HorizontalBars()` - Horizontal bar chart
- `SectionCard()` - Card for content sections

---

## 🎨 Patterns & Techniques Used

### 1. **Structured Layout**
```csharp
container.Page(page =>
{
    page.Size(PageSizes.A4);
    page.Margin(40);
    
    page.Header().Element(ComposeHeader);
    page.Content().Element(ComposeContent);
    page.Footer().Element(ComposeFooter);
});
```

### 2. **Cards for KPIs**
```csharp
container.Background(SurfaceColor)
    .Border(1)
    .BorderColor(BorderColor)
    .Padding(14)
    .Column(col =>
    {
        col.Item().Text(title).Bold().FontSize(11);
        col.Item().PaddingTop(8).Text(value).FontSize(20).Bold().FontColor(accent);
    });
```

### 3. **Tables**
```csharp
container.Table(table =>
{
    table.ColumnsDefinition(cols =>
    {
        cols.RelativeColumn();
        cols.RelativeColumn();
        cols.ConstantColumn(100);
    });
    
    table.Header(header => { /* ... */ });
    
    foreach (var item in items)
    {
        table.Cell().Text(item.Name);
        table.Cell().Text(item.Value);
        table.Cell().AlignRight().Text(item.Amount);
    }
});
```

### 4. **Bar Charts**
```csharp
foreach (var bar in bars)
{
    var ratio = bar.Value / maxValue;
    row.RelativeItem()
        .Column(col =>
        {
            col.Item().AlignCenter().Text(bar.Label);
            col.Item().Height(barHeight).Background(bar.Color);
        });
}
```

### 5. **Colors and Styling**
```csharp
private static readonly Color PrimaryColor = Color.FromHex("#0f4c81");
private static readonly Color GreenColor = Color.FromHex("#137333");
private static readonly Color RedColor = Color.FromHex("#c5221f");
private static readonly Color LightBg = Color.FromHex("#f1f3f4");
```

---

## 📖 How to Use These References

### For Developers
1. Read the reference templates to understand the structure
2. Copy and adapt the structures for your templates
3. Use `SharedReportLayout` and `SharedChartComponents` as a base
4. Keep the same structure (IDocument, GetMetadata, Compose)

### For Product/Design
1. Review the examples to understand the capabilities
2. Adapt colors, fonts, and layouts
3. Print vs digital considerations
4. Optimize for the formats (PDF, Excel)

---

## 🚀 Templates to Create (Sprint B1-B4)

### Core Templates (Must-Have)
- [ ] InvoiceDocument (based on the example)
- [ ] FinancialReportDocument (based on the example)
- [ ] SimpleBulletReport (minimalist template)

### Extended Templates (Nice-to-Have)
- [ ] ProductCatalogDocument
- [ ] SalesPerformanceDocument
- [ ] OperationsSnapshotDocument

---

## 📚 QuestPDF References

- **Official Documentation**: https://www.questpdf.com/
- **Community Discord**: https://discord.gg/questpdf
- **GitHub**: https://github.com/QuestPDF/QuestPDF
- **Examples**: https://github.com/QuestPDF/QuestPDF/tree/main/Examples

---

**Last Updated**: April 21, 2026  
**Templates in Repository**: 5 reference templates included
