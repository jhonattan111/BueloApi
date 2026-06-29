# Sprint B1: Core Rendering Engine - QuestPDF Foundation

## 🎯 Objective
Establish a solid foundation for report rendering using pure QuestPDF with C#. Completely remove the complexity of the Buelo DSL and focus on well-structured C# IDocument templates.

## ✅ Tasks

### Backend

#### 1. TemplateEngine Refactor
- [x] Remove all references to BueloDsl (parser, compiler, engine)
- [x] Update TemplateMode to use only `FullClass`
- [x] Simplify RenderAsync() to handle pure C#
- [ ] Implement basic template validation (IDocument check)
- [ ] Setup for dynamic compilation with Roslyn (future)

#### 2. Data Contract
- [x] Remove TemplatePath and DataSourcePath from ReportRequest
- [x] Simplify ReportRequest to: Template, FileName, Data, PageSettings
- [x] Update defaults in TemplateRecord and ValidationResult
- [ ] Create simple data model examples

#### 3. ReportController Validation
- [ ] Test POST /api/report/validate with a valid C# template
- [ ] Test POST /api/report/validate with an invalid C# template
- [ ] Verify that errors are returned correctly

### Frontend

#### 1. Report Editor UI
- [ ] Update Monaco Editor for C# language highlighting
- [ ] Remove references to buelo-language
- [ ] Show "C# Template" mode instead of "Buelo DSL"

#### 2. Report Settings Panel
- [ ] Implement Report Settings page
  - Data Source selector (JSON)
  - Page Size dropdown (A4, Letter, etc)
  - Margin controls (top, right, bottom, left)
  - Orientation toggle (Portrait/Landscape)
  - Background color picker
  - Default font size input

## 📚 Output References
- Simple QuestPDF template example: `InvoiceDocument.cs`
- Example with KPIs: `FinancialDashboardDocument.cs`
- Example with tables: see SalesPerformanceDocument reference

## 🚀 Next Sprint
Sprint B2: Report API & Data Flow (mock data rendering)
