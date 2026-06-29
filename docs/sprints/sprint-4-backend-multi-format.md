# Sprint B4: Multi-Format Output & Advanced Rendering

## 🎯 Objective
Extend rendering capabilities to multiple formats (PDF, Excel) and optimize performance with caching and background compilation.

## ✅ Tasks

### Backend

#### 1. Output Renderer Registry
- [ ] Verify IOutputRenderer interface
- [ ] Register PdfRenderer and ExcelRenderer
- [ ] Add support for more formats as needed

#### 2. Excel Rendering
- [ ] Implement ExcelRenderer using ClosedXML
- [ ] Create Excel templates equivalent to the PDFs
- [ ] Test rendering of complex data in Excel

#### 3. Performance Optimization
- [ ] Implement cache of compiled templates
- [ ] Lazy loading of large templates
- [ ] Batch rendering for multiple reports

#### 4. ReportController Format Support
- [ ] Query parameter `format=pdf|excel` on the render endpoint
- [ ] Correct Content-Type per format
- [ ] Correct filename with extension

### Frontend

#### 1. Export Options
- [ ] "Export As" dropdown (PDF, Excel)
- [ ] Direct download after click
- [ ] Preview for the selected format

#### 2. Template Preview by Format
- [ ] Show preview in PDF vs Excel
- [ ] Different layouts per format
- [ ] Template validation per format

## 📊 Supported Output Formats
- PDF (QuestPDF) - ✅ Primary
- Excel (ClosedXML) - Implementation
- HTML (Future)
- CSV (Future)

## 🔄 Render Pipeline
```
Template (C#) 
  ↓ Compile
  ↓ Instantiate IDocument
  ↓ Bind Data
  ↓ Select Renderer (PDF/Excel/etc)
  ↓ Render
  ↓ Return bytes + content-type
```

## ✅ Sprint Completion Criteria
- [ ] PDF rendering working 100%
- [ ] Excel rendering working
- [ ] Format selector in the frontend
- [ ] Acceptable performance (<2s for a medium report)
- [ ] Cache implemented

## 🚀 Future Sprints
- Sprint B5: Template Versioning & History
- Sprint B6: Scheduling & Batch Reports
- Sprint B7: Advanced PageSettings (headers, footers, watermarks)
