# Sprint B3: Global Artefacts & Data Sources

## 🎯 Objective
Implement a Global Artefacts system to store and manage JSON data sources, allowing templates to reference external data in a centralized way.

## ✅ Tasks

### Backend

#### 1. GlobalArtefactStore Enhancement
- [ ] Implement persistent storage of global artefacts
- [ ] Allow creating/updating/deleting artefacts
- [ ] Support JSON artefacts as data sources

#### 2. GlobalArtefactsController
- [ ] GET /api/artefacts - list all
- [ ] GET /api/artefacts/{id} - get a specific one
- [ ] POST /api/artefacts - create new
- [ ] PUT /api/artefacts/{id} - update
- [ ] DELETE /api/artefacts/{id} - delete

#### 3. Data Binding
- [ ] Allow a template to reference Global Artefacts by ID
- [ ] Implement reference resolution in RenderAsync()
- [ ] Validate that the artefact exists before rendering

#### 4. Environment-Specific Data
- [ ] Support data source override per environment
- [ ] Development vs Production data sources
- [ ] Environment configuration in appsettings

### Frontend

#### 1. Data Sources Panel
- [ ] List available Global Artefacts
- [ ] Inline editor for JSON data sources
- [ ] Load/save data sources
- [ ] Validate JSON syntax

#### 2. Template Data Binding
- [ ] Allow selecting a data source for a template
- [ ] Dropdown with available artefacts
- [ ] Preview with the selected data source

#### 3. Artefact Manager
- [ ] Interface to create/edit global artefacts
- [ ] Upload of JSON files
- [ ] Test an artefact before saving

## 🗂️ Structure Example
```
GlobalArtefacts/
├── invoices.json (list of invoices)
├── products.json (product catalog)
├── employees.json (employee data)
└── financial-2024.json (financial data)
```

## 📋 ReportRequest with Data Binding
```csharp
new ReportRequest {
    Template = "...", // C# template code
    Data = new { GlobalArtefactId = "invoices.json", Filter = "status=pending" }
}
```

## 🚀 Next Sprint
Sprint B4: Multi-Format Output (PDF, Excel, etc)
