# Sprint B3 (Backend) — Global Artefacts & Data Sources

## Goal
Implement a Global Artefacts system to store and manage JSON data sources, allowing templates to
reference external data in a centralized way.

## Status
`[x] done`

## Dependencies
- None

## Scope
- [ ] Implement persistent storage of global artefacts
- [ ] Allow creating/updating/deleting artefacts
- [ ] Support JSON artefacts as data sources
- [ ] GET /api/artefacts - list all
- [ ] GET /api/artefacts/{id} - get a specific one
- [ ] POST /api/artefacts - create new
- [ ] PUT /api/artefacts/{id} - update
- [ ] DELETE /api/artefacts/{id} - delete
- [ ] Allow a template to reference Global Artefacts by ID
- [ ] Implement reference resolution in RenderAsync()
- [ ] Validate that the artefact exists before rendering
- [ ] Support data source override per environment
- [ ] Development vs Production data sources
- [ ] Environment configuration in appsettings

**Frontend (companion sprint):**
- [ ] List available Global Artefacts
- [ ] Inline editor for JSON data sources
- [ ] Load/save data sources
- [ ] Validate JSON syntax
- [ ] Allow selecting a data source for a template
- [ ] Dropdown with available artefacts
- [ ] Preview with the selected data source
- [ ] Interface to create/edit global artefacts
- [ ] Upload of JSON files
- [ ] Test an artefact before saving

## Notes

Structure example:
```
GlobalArtefacts/
├── invoices.json (list of invoices)
├── products.json (product catalog)
├── employees.json (employee data)
└── financial-2024.json (financial data)
```

`ReportRequest` with data binding:
```csharp
new ReportRequest {
    Template = "...", // C# template code
    Data = new { GlobalArtefactId = "invoices.json", Filter = "status=pending" }
}
```

Next sprint at the time of writing: Sprint B4 — Multi-Format Output (PDF, Excel, etc).
