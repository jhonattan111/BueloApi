# Sprint history — Buelo Backend

## Overview
Historical log of backend sprints. Each sprint has its own file in `sprints/`. **Not current state** —
[`../CLAUDE.md`](../CLAUDE.md) wins on any conflict.

> **Current architecture**: two authoring paths — **declarative YAML** (primary, no Roslyn — see
> [`reference/`](reference/)) and **C# `IDocument`** (the escape hatch, compiled with Roslyn). This
> index stops at Sprint 22/B5; the declarative engine, DB-backed persistence, and infra work that
> followed are tracked in the umbrella repo's `docs/handoff.md`, not as numbered sprints.

## Sprint Index

### ✅ Archived Sprints (DSL era — removed)

| Sprint | File | Goal | Status |
|--------|------|------|--------|
| 6 | _archived/sprint-6-sections-mode.md | Sections/Partial modes, @import directive | `[x] archived` |
| 7 | _archived/sprint-7-backend-dsl-foundation.md | DSL foundation, @data/@settings directives | `[x] archived` |
| 8 | [sprint-8-backend-template-bundle.md](sprints/sprint-8-backend-template-bundle.md) | TemplateBundle artefacts, FileSystemTemplateStore, export/import ZIP | `[x] done` |
| 9 | [sprint-9-backend-helpers-versioning.md](sprints/sprint-9-backend-helpers-versioning.md) | Dynamic @helper scripts, template versioning + restore | `[x] done` |
| 13 | [sprint-13-backend-global-artefact-store.md](sprints/sprint-13-backend-global-artefact-store.md) | Global shared files (employee.json, formatters.csx); file extension conventions | `[x] done` |
| 14 | _archived/sprint-14-backend-buelo-dsl-redesign.md | YAML-like .buelo DSL (BueloDslParser, BueloDslCompiler) | `[x] archived` |
| 15 | [sprint-15-backend-project-file.md](sprints/sprint-15-backend-project-file.md) | project.bueloproject — workspace settings, global mock data, page defaults cascade | `[x] done` |
| 16 | [sprint-16-backend-file-validation.md](sprints/sprint-16-backend-file-validation.md) | Per-file-type validation: .json, .cs/.csx via Roslyn syntax check | `[x] done` |
| 17 | [sprint-17-backend-extensible-renderers.md](sprints/sprint-17-backend-extensible-renderers.md) | IOutputRenderer abstraction; PdfRenderer + ExcelRenderer (ClosedXML); ?format= param | `[x] done` |
| 18 | [sprint-18-backend-inline-project-config.md](sprints/sprint-18-backend-inline-project-config.md) | OutputFormat per TemplateRecord; PageSettings cascade | `[x] done` |
| 19 | [sprint-19-backend-project-validation.md](sprints/sprint-19-backend-project-validation.md) | POST /api/validate/project — full workspace validation | `[x] done` |
| 20 | [sprint-20-backend-remove-obsolete.md](sprints/sprint-20-backend-remove-obsolete.md) | Remove Sections/Partial modes, BueloDsl, SectionsTemplateParser, ZIP bundle endpoints | `[x] done` |
| 21 | [sprint-21-backend-workspace-filesystem.md](sprints/sprint-21-backend-workspace-filesystem.md) | Workspace filesystem APIs (folders/files), dataSourcePath binding, import resolver | `[x] done` |

### 🚀 Active Sprints (QuestPDF C# era)

| Sprint | File | Goal | Status |
|--------|------|------|--------|
| B1 | [sprint-1-backend-core-engine.md](sprints/sprint-1-backend-core-engine.md) | TemplateEngine with C# IDocument; Roslyn compilation; validation; PageSettings | `[x] done` |
| B2 | [sprint-2-backend-api-mockdata.md](sprints/sprint-2-backend-api-mockdata.md) | Complete ReportController endpoints; mock data flow; render pipeline | `[x] done` |
| B3 | [sprint-3-backend-global-artefacts.md](sprints/sprint-3-backend-global-artefacts.md) | GlobalArtefactStore as JSON data sources; data binding in templates | `[x] done` |
| B4 | [sprint-4-backend-multi-format.md](sprints/sprint-4-backend-multi-format.md) | Multi-format output (PDF + Excel); OutputRendererRegistry; performance | `[x] done` |
| B5 | [sprint-22-backend-typed-data-intellisense.md](sprints/sprint-22-backend-typed-data-intellisense.md) | JsonTypeInferrer; C# type declarations from JSON; `GET /api/workspace/types` | `[x] done` |

## Dependency Chain

```
[Sprints 6–21 archived/completed — DSL era]
    ↓
Sprint B1 (Core Rendering Engine — C# IDocument + Roslyn)
    ↓
Sprint B2 (Report API + Mock Data)
    ↓
Sprint B3 (Global Artefacts + Data Sources)
    ↓
Sprint B4 (Multi-Format Output)
    ↓
Sprint B5 (Typed Data IntelliSense — JsonTypeInferrer + /api/workspace/types)
    ↓
[Frontend sprints — see BueloWeb/docs/sprint-history.md]
```

## Project structure & conventions

Superseded by [`../CLAUDE.md`](../CLAUDE.md) — its "Solution structure" and "Conventions" sections are
the current, maintained version of what used to be duplicated here. Sprint-specific process note that
still applies: each sprint changes **Contracts → Engine → Api** in that order, and `dotnet build` +
`dotnet test` must pass before marking a sprint done.
