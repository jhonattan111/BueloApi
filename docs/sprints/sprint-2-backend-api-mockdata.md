# Sprint B2 (Backend) — Report API & Mock Data Flow

## Goal
Implement the complete rendering flow: JSON data → C# template → PDF using pure QuestPDF, with mock
data for validation.

## Status
`[x] done`

## Dependencies
- None

## Scope
**Template Storage with MockData:**
- [ ] Create a few example templates with MockData:
  - Invoice template with fictional data
  - Financial Dashboard with KPIs
  - Operations Snapshot with metrics
- [ ] Verify that MockData is being persisted correctly
- [ ] Implement MockData validation against the expected format

**ReportController Endpoints:**
- [x] POST /api/report/validate - template validation
- [ ] POST /api/report/render - rendering with mock data
  - Receive: Template, Data, PageSettings
  - Return: PDF bytes
- [ ] GET /api/report/templates - list templates
- [ ] GET /api/report/templates/{id} - get a specific template

**C# Template Validation:**
- [ ] Verify template compilation (without dynamic Roslyn for now)
- [ ] Return clear syntax errors
- [ ] Validate presence of the IDocument interface

**Frontend (companion sprint):**
- [ ] Create tabs: Editor, Preview, Settings
- [ ] Monaco Editor with C# syntax highlighting
- [ ] Preview panel showing the result in real time
- [ ] On-keystroke validation
- [ ] Form to configure: data source (JSON selector), page size, margins, colors
- [ ] Preview of the applied settings
- [ ] Load MockData from the template
- [ ] Allow editing MockData inline
- [ ] Update the preview automatically

## Notes

Flow example:
```
1. User writes a C# template (implements IDocument)
2. User clicks "Preview"
3. Frontend validates the template via /api/report/validate
4. Frontend sends Template + MockData to /api/report/render
5. Backend renders and returns PDF bytes
6. Frontend displays the PDF preview
```

Next sprint at the time of writing: Sprint B3 — Global Artefacts & Data Sources (real data).
