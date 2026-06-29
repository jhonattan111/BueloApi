# CLAUDE.md — BueloApi

Guide for AI agents (Claude Code) working in this repository. It is the **canonical document** of the current architecture — in case of divergence with older docs (`ARCHITECTURE.md`, `docs/`), this file wins.

## What it is

`BueloApi` is the **report generation API** of the Buelo product. It has **two authoring paths**:

1. **C# (`IDocument`)** — receives C# code at runtime, compiles it with **Roslyn**, instantiates a `QuestPDF.Infrastructure.IDocument` class, and returns **PDF** (or **Excel** via ClosedXML). It is the full-power "escape hatch".
2. **Declarative (YAML)** — receives a declarative definition, lowers it to a typed IR (`BueloDocument`), and composes via QuestPDF — **without Roslyn**. It is the primary authoring path. See [`docs/blueprint-schema-canonico.md`](../docs/blueprint-schema-canonico.md) and the **Declarative engine** section below.

> It is part of the Buelo product, alongside the front end [`BueloWeb`](../BueloWeb). The umbrella repo is [`Buelo`](..) (submodules). The front end consumes this API at `http://localhost:5238`.

## Stack

- **ASP.NET Core 10** (`net10.0`), C# with primary constructors
- **QuestPDF** (Community license) — PDF layout
- **Microsoft.CodeAnalysis.CSharp** (Roslyn) — template compilation at runtime
- **ClosedXML** — Excel output
- Tests: **xUnit** in `Buelo.Tests`

## Solution structure (`Buelo.slnx`)

```
Buelo.Contracts   ← interfaces, models, enums. NO business logic, NO QuestPDF, NO Roslyn
Buelo.Engine      ← Roslyn compilation, render (PDF/Excel), stores, validators
Buelo.Api         ← ASP.NET Core controllers + Program.cs
Buelo.Tests       ← xUnit (Engine/ and Api/)
```

**Dependency direction (never invert):** `Buelo.Api → Buelo.Engine → Buelo.Contracts`

## Commands

```bash
dotnet build                          # from the repo root (uses Buelo.slnx)
dotnet test                           # runs Buelo.Tests
dotnet run --project Buelo.Api        # starts the API at http://localhost:5238
```

**After any change, `dotnet build` and `dotnet test` must pass with zero errors before considering the task done.**

**Commit & push:** with `dotnet build` + `dotnet test` green, run `git commit` and `git push` (don't pile up local work); then bump the pointer in the umbrella repo and push there too. If any test fails, fix it before committing/pushing. See [`../CLAUDE.md`](../CLAUDE.md) (§Commit & push policy).

## Core concept: templates are C# `IDocument` classes

A template is a complete, compilable C# class implementing `QuestPDF.Infrastructure.IDocument`. The constructor receives the data (and optionally `PageSettings`):

```csharp
public class InvoiceDocument : IDocument
{
    private readonly dynamic _data;
    public InvoiceDocument(dynamic data) => _data = data;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container) =>
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Content().Text((string)_data.name);
        });
}
```

`TemplateMode` has **only** `FullClass = 1`. The old `.buelo` DSL and the `Sections`/`Builder`/`Partial` modes and the `IReport` interface **have been removed** — do not reintroduce them.

## Render pipeline (`TemplateEngine`)

`RenderAsync(template, data, mode = FullClass, pageSettings?)`:

1. `ConvertToDynamic(data)` — `object` → `ExpandoObject` via System.Text.Json
2. `CompileTemplate(source)` — `CSharpCompilation` (Release, nullable enable) → emits assembly in memory; errors become `InvalidOperationException` with line
3. `FindDocumentType` — first non-abstract class that implements `IDocument`
4. `CreateDocumentInstance` — picks the ctor with the **most parameters**; parameters of type `PageSettings` receive the settings, the rest receive the data (`ExpandoObject` directly, or JSON round-trip into a typed model)
5. `document.GeneratePdf()` → `byte[]`

`RenderTemplateAsync(record, data?, pageSettings?)`: uses `record.MockData` if `data` is null; `MergeSettings` = `request ?? template ?? Default`.

`ValidateAsync` compiles and returns `ValidationResult { Valid, Errors[] }` (line/column) **without** executing — it never throws.

## Declarative engine (YAML → IR → QuestPDF)

Pipeline: **YAML → `DeclarativeParser` (YamlDotNet) → `DeclarativeInterpreter`/`DeclarativeLowering` (evaluates `{{ }}`, resolves directives and styles) → `BueloDocument` (typed IR) → `BueloDocumentRenderer` (composes QuestPDF) → PDF.** Code in `Buelo.Engine/Declarative/`, `Buelo.Engine/Ir/`, `Buelo.Engine/Renderers/BueloDocumentRenderer.cs`. Orchestrator: `DeclarativeReportEngine` (singleton).

- **Kinds** (`kind:` at the top of the YAML): `report` (renderable), `component` (params + slots + `use`/`with`), `styles` (classes + `extends`), `theme`, `formats` (masks), `lib` (named expressions), `validator` (3 tiers). Modules indexed by `Declarative/Modules/ModuleRegistry`.
- **Layout blocks** (`Declarative/DeclarativeAst.cs`): `text`, `markdown`, `table` (data-oriented, `groupBy`, footer with aggregation), `row`, `column`, `card`/`panel`, `image`, `spacer`, `divider`, `pageBreak`; `header`/`content`/`footer` bands; context vars `now`/`today`/`page`/`pageCount`/`report.name`.
- **`{{ }}` expressions** (`Declarative/Expressions/`): lexer + recursive parser + evaluator. Arithmetic, comparison, logical, ternary, `??`, pipes, calls; stdlib (`moeda`/`data`/`cpf`/`cnpj`/`percent`/`upper`/`join`/`mask`/`if`/`coalesce`…) + aggregation `sum`/`avg`/`count`/`min`/`max`.
- **Definition persistence:** `IDefinitionStore` (`{kind}/{name}.yml`) — `FileSystemDefinitionStore` (default, git-friendly) and `InMemoryDefinitionStore` (tests). Root via config `Buelo:DefinitionStorePath` (fallback `definitions`).
- **Operational (EF Core):** `BueloDbContext` + `IRenderLog` (render history). SQLite (dev) / Postgres (prod) via `Buelo:Database:Provider`. `AddBueloPersistence(config)` + `EnsureBueloDatabase()` at startup. Default without DB = `NullRenderLog`.
- **Eject:** `CSharpEjector` generates a C# `IDocument` from the IR (declarative→code graduation).
- **Examples:** `Buelo.Api/definitions/` (reports `hello`/`invoice`/`colaboradores` + modules + mock data in `data/`). See `Buelo.Api/definitions/README.md`.

## API surface (real routes)

| Controller | Base | Routes |
|---|---|---|
| `ReportController` | `api/report` | C#: `POST render`, `POST validate`, `POST render/{id}`, `POST preview/{id}`, `GET formats` · Declarative: `POST render-declarative`, `POST render-stored/{name}`, `POST eject` |
| `SchemasController` | `api/schemas` | `GET` (lists kinds), `GET {kind}` (JSON Schema of the kind) |
| `DeclarativeValidationController` | `api/validate-data` | `POST` (validates a value against a `kind: validator`) |
| `RenderHistoryController` | `api/render-history` | `GET` (render history from the EF store) |
| `TemplatesController` | `api/templates` | CRUD + `{id}/artefacts[/{name}]`, `{id}/files`, `{id}/versions[/{n}[/restore]]` |
| `GlobalArtefactsController` | `api/artefacts` | CRUD + `GET by-name/{name}` |
| `WorkspaceController` | `api/workspace` | `GET tree`, `POST folders`, `POST/GET/PUT files[/content]`, `DELETE nodes`, `GET types` |
| `ValidateController` | `api/validate` | `POST` (1 file), `POST project` |
| *(minimal)* | `/ping` | `GET` (public liveness; open even with API key) |

Render/preview return `application/pdf` (or Excel). Use `?format=` when applicable. Errors: `404` (id not found), `400` (no data).

## Main contracts (`Buelo.Contracts`)

- **`TemplateRecord`** — `Id, Name, Description, Template (C#), Mode, DataSchema, MockData, DefaultFileName, OutputFormat (Pdf|Excel), PageSettings, CreatedAt, UpdatedAt, Artefacts[]`
- **`TemplateArtefact`** — `Path?, Name, Extension, Content` (attached files: mock data, schema, `.cs` helpers)
- **`ReportRequest`** — `Template, FileName, Data, Mode, PageSettings?`
- **`PageSettings`** — size, margins (cm), colors, watermark, font, header/footer. `PageSettings.Default()` = A4 / 2cm
- **`IHelperRegistry`** — `FormatCurrency`, `FormatDate` (default: `DefaultHelperRegistry`)
- Stores: `ITemplateStore`, `IGlobalArtefactStore`, `IWorkspaceStore`, `IWorkspaceFileEnumerator`

## DI registration (`AddBueloEngine`)

`builder.Services.AddBueloEngine();` registers as singletons: `TemplateEngine`, `ITemplateStore → InMemoryTemplateStore`, `IGlobalArtefactStore → InMemory...`, `IWorkspaceStore/Enumerator → FileSystem...`, validators (`Json`, `Csharp`) + `FileValidatorRegistry`, renderers (`Pdf`, `Excel`) + `OutputRendererRegistry`, `IHelperRegistry → DefaultHelperRegistry`.

- Uses `TryAdd` → register your own `IHelperRegistry`/`ITemplateStore` **first** to override.
- Disk persistence: `AddBueloFileSystemStore()` (opt-in); root via `appsettings` `Buelo:TemplateStorePath` (fallback: `./templates`).

## Conventions

- **Layers in order:** a change is born in `Contracts` → `Engine` → `Api`. Never reference `Engine` from `Contracts`.
- Controllers use **primary constructor injection**; return explicit types (`Ok`, `NotFound`, `BadRequest`).
- `ITemplateStore` is **async** in everything (`Task<T>`), even the in-memory impl.
- Every new endpoint needs a test in `Buelo.Tests/Api/` (happy path + not found + bad input). Engine components: test in `Buelo.Tests/Engine/`.
- `Program.cs`: `AddBueloEngine()` + `AddBueloPersistence(config)`; CORS for `http://localhost:5173`; OpenAPI only in Development; QuestPDF Community license; `EnsureBueloDatabase()`; public `/ping`; `ApiKeyMiddleware` (opt-in gate).
- **Config (env/appsettings):** `Buelo:ApiKey` (Bearer opt-in; empty = auth off), `Buelo:RenderTimeoutSeconds` (default 30; 0 disables), `Buelo:DefinitionStorePath` (default `definitions`), `Buelo:Database:Provider` (`sqlite`|`postgres`) + `Buelo:Database:ConnectionString`.
- **Roslyn assembly cache** by content hash in `TemplateEngine` (repeated renders of the same C# template skip recompilation).
- **Self-hosted model:** no sandbox/multi-tenant; whoever has the API key is trusted (see blueprint).

## History and reference

`docs/` keeps the sprint history and detailed guides (`.buelo` DSL era → C#/QuestPDF era). It is **historical reference**, not the current state. `ARCHITECTURE.md` at the root has the rationale for the redesign.
