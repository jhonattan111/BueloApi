# API reference — declarative reports & page settings

This folder is the deep reference for the two things too detailed for [`CLAUDE.md`](../../CLAUDE.md):
the **declarative YAML format** (the primary authoring path) and **`PageSettings`** (the C# path's
page configuration). `CLAUDE.md` is still canonical on architecture and conventions — this is where
you come for "what exact shape does this block/property have."

## Declarative YAML

A declarative report is YAML that the engine lowers to a typed IR (`BueloDocument`) and renders with
QuestPDF — **no C# involved**. Authoritative implementation: `Buelo.Engine/Declarative/DeclarativeAst.cs`
(blocks/props) and `Buelo.Engine/Declarative/Expressions/` (the `{{ }}` language). JSON Schemas per
`kind` are served at `GET /api/schemas/{kind}` and drive the editor's autocomplete.

| File | Covers |
|---|---|
| [`report.md`](report.md) | Top-level shape (`kind: report`, `meta.page`, bands), full examples, a generation checklist for AI-authored reports |
| [`blocks.md`](blocks.md) | Every layout block (`text`, `table`, `row`, `card`, …), the table's grouping/aggregation, the shared `<Style>` object |
| [`expressions.md`](expressions.md) | The `{{ }}` expression language, scopes/context variables, the standard library, pipes |
| [`modules.md`](modules.md) | The other kinds — `component`, `styles`, `theme`, `formats`, `lib`, `validator` — name resolution, `import`/`use`/`with`, the 3-tier validator model |

**How a report is rendered:** the editor sends the YAML + a JSON **data** object to
`POST /api/report/render-declarative?format=pdf|excel`. Dynamic values come from `{{ ... }}`
expressions evaluated against `data`. When a report `import:`s modules, the editor gathers the
workspace's `*.{styles,component,theme,formats,lib,validator}.yml` definitions and sends them in the
request's `Modules` field, so `import:`/`use:`/`class:` resolve (see [`modules.md`](modules.md)). For
a standalone report (e.g. one an AI generates without shipping companion module files), prefer inline
`style: { ... }` and skip `import:` — a self-contained report always renders on its own.

## C# path

| File | Covers |
|---|---|
| [`page-settings.md`](page-settings.md) | The `PageSettings` contract, precedence rules, and how a template opts into it |

## See also

- [`../../CLAUDE.md`](../../CLAUDE.md) — architecture, DI wiring, both engines' pipelines
- [`../workflows.md`](../workflows.md) — how to add an endpoint / declarative block / stdlib function
- [`../../../docs/blueprint-schema-canonico.md`](../../../docs/blueprint-schema-canonico.md) (umbrella repo) — the original design rationale and IR mapping behind this format
