# Sprint B4 (Backend) — Multi-Format Output & Advanced Rendering

## Goal
Extend rendering capabilities to multiple formats (PDF, Excel) and optimize performance with
caching and background compilation.

## Status
`[x] done`

## Dependencies
- None

## Scope
- [ ] Verify IOutputRenderer interface
- [ ] Register PdfRenderer and ExcelRenderer
- [ ] Add support for more formats as needed
- [ ] Implement ExcelRenderer using ClosedXML
- [ ] Create Excel templates equivalent to the PDFs
- [ ] Test rendering of complex data in Excel
- [ ] Implement cache of compiled templates
- [ ] Lazy loading of large templates
- [ ] Batch rendering for multiple reports
- [ ] Query parameter `format=pdf|excel` on the render endpoint
- [ ] Correct Content-Type per format
- [ ] Correct filename with extension

**Frontend (companion sprint):**
- [ ] "Export As" dropdown (PDF, Excel)
- [ ] Direct download after click
- [ ] Preview for the selected format
- [ ] Show preview in PDF vs Excel
- [ ] Different layouts per format
- [ ] Template validation per format

**Sprint completion criteria (as originally tracked):**
- [ ] PDF rendering working 100%
- [ ] Excel rendering working
- [ ] Format selector in the frontend
- [ ] Acceptable performance (<2s for a medium report)
- [ ] Cache implemented

## Notes

Supported output formats at the time of writing: PDF (QuestPDF) — primary; Excel (ClosedXML) —
implementation; HTML (future); CSV (future).

Render pipeline:
```
Template (C#)
  ↓ Compile
  ↓ Instantiate IDocument
  ↓ Bind Data
  ↓ Select Renderer (PDF/Excel/etc)
  ↓ Render
  ↓ Return bytes + content-type
```

Future sprints planned at the time of writing: Sprint B5 (Template Versioning & History), Sprint
B6 (Scheduling & Batch Reports), Sprint B7 (Advanced PageSettings — headers, footers, watermarks).
