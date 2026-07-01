# Workflows — how to add things

Short, current "how do I..." recipes. For *why* things are shaped this way, see
[`../CLAUDE.md`](../CLAUDE.md) — that's the canonical doc and wins on any conflict with this file.

## Add a new endpoint

1. Add the route to the relevant controller (primary-constructor injection, explicit return types —
   `Ok(...)`, `NotFound()`, `BadRequest(...)`).
2. New request/response shapes go in `Buelo.Contracts` if reusable, otherwise local records in the
   controller.
3. Call into `TemplateEngine`, `DeclarativeReportEngine`, or the relevant store via injection.
4. Add a test in `Buelo.Tests/Api/` covering at least: happy path, not found, bad input.

## Add a new declarative block (e.g. a `chart` block)

1. Add the AST node/props to `Buelo.Engine/Declarative/DeclarativeAst.cs`.
2. Parse it in `DeclarativeParser.cs` (YamlDotNet mapping).
3. Lower it in `DeclarativeLowering.cs` / `DeclarativeInterpreter.cs` into a `BueloDocument` IR node
   (`Buelo.Engine/Ir/`) — this is where `{{ }}` expressions get evaluated and directives resolved, so
   nothing pending reaches the renderer.
4. Render the IR node in `Buelo.Engine/Renderers/BueloDocumentRenderer.cs` (the QuestPDF composition).
5. Update the block's JSON Schema in `Buelo.Engine/Declarative/Schema/DeclarativeSchemas.cs` so the
   editor's `monaco-yaml` autocomplete picks it up (served at `GET /api/schemas/{kind}`).
6. Document the new block in [`reference/blocks.md`](reference/blocks.md).
7. Cover it in `Buelo.Tests/Engine/` — parse → lower → render (or at least parse → lower; a full PDF
   assertion isn't usually necessary).

## Add a stdlib expression function (e.g. a new formatter)

1. Add the case to `Buelo.Engine/Declarative/Expressions/ExpressionFunctions.cs` (a switch on the
   function name — see `"currency"`/`"sum"` for the pattern; aggregation functions take a sub-expression
   evaluated per array element via `Buelo.Engine/Declarative/Expressions/ExpressionEngine.cs`).
2. Add a test in `Buelo.Tests/Engine/` (parse `{{ fn(...) }}` and assert the evaluated result).
3. Document it in [`reference/expressions.md`](reference/expressions.md).

## Add an alternative `ITemplateStore` / `IWorkspaceStore`

1. Implement the interface in `Buelo.Engine/` (for file-system/in-memory variants) or `Buelo.Persistence/`
   (for a new EF-backed store).
2. Add a registration extension method (see `AddBueloFileSystemStore()` in `EngineExtensions.cs` for the
   pattern) — **do not** register it as the default; `Program.cs` wires the default explicitly via
   `AddBueloEngine()` + `AddBueloPersistence(config)`.
3. Cover it with round-trip tests in `Buelo.Tests/Engine/` (or `Buelo.Tests/Persistence/` for EF stores).

## Add or update an EF Core migration

Both providers ship separate migrations (provider-specific SQL) — add to both after a model change:

```bash
dotnet ef migrations add <Name> -p Buelo.Persistence -s Buelo.Persistence               # SQLite
dotnet ef migrations add <Name> -p Buelo.Persistence.Postgres -s Buelo.Persistence.Postgres  # PostgreSQL
```

Works because neither `Buelo.Persistence*` project references Roslyn (`dotnet ef` can load the assembly
cleanly). `EnsureBueloDatabase()` runs `Migrate()` at startup when migrations are present for the active
provider.
