# Buelo API

The report-generation API of the **Buelo** platform. You give it a report definition plus data
(JSON); it returns a **PDF** or **Excel** file. It is self-hosted: run your own instance, point your
services at it.

There are **two authoring paths**, both rendered with [QuestPDF](https://www.questpdf.com/):

1. **Declarative (YAML)** — the primary path. A `kind: report` YAML document is parsed, lowered to a
   typed layout IR (`BueloDocument`), and composed by QuestPDF. **No code compilation.** Safe and
   fast; ideal for an API and for AI-generated reports.
2. **C# (`IDocument`)** — the full-power escape hatch. You send a C# class implementing
   `QuestPDF.Infrastructure.IDocument`; the API compiles it at runtime with **Roslyn** and renders
   it. Anything QuestPDF can do, you can do.

> Part of the Buelo product, alongside the editor front end [`BueloWeb`](../BueloWeb) (which consumes
> this API at `http://localhost:5238`). Umbrella repo: [`Buelo`](..) (git submodules).
>
> The **canonical engineering doc is [`CLAUDE.md`](CLAUDE.md)**; the declarative format has a
> dedicated reference at [`docs/declarative-format.md`](docs/declarative-format.md).

## Stack

- **ASP.NET Core 10** (`net10.0`), C#
- **QuestPDF** (Community license) — PDF layout
- **Microsoft.CodeAnalysis.CSharp** (Roslyn) — runtime compilation of C# templates
- **ClosedXML** — Excel output
- **YamlDotNet** — declarative parsing
- **Entity Framework Core** — persistence (SQLite default / PostgreSQL)
- Tests: **xUnit**

## Solution structure (`Buelo.slnx`)

```
Buelo.Contracts            interfaces, models, enums. No business logic, no QuestPDF/Roslyn/EF
Buelo.Engine               Roslyn compilation, declarative engine, PDF/Excel renderers, validators
Buelo.Persistence          EF Core: DbContext + SQLite migrations + DB-backed stores
Buelo.Persistence.Postgres EF Core: PostgreSQL migrations assembly
Buelo.Api                  ASP.NET Core controllers + Program.cs
Buelo.Tests                xUnit (Engine/, Api/, Persistence/)
```

Dependency direction (never inverted): `Buelo.Api → { Buelo.Engine, Buelo.Persistence } → Buelo.Contracts`.
`Engine` and `Persistence` are siblings — `Engine` owns Roslyn, `Persistence` owns EF; keeping them
apart is what lets the EF migration tooling coexist with Roslyn.

## Getting started

### Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download)

### Run

```bash
dotnet run --project Buelo.Api      # → http://localhost:5238
```

On first start the database is created/migrated and the bundled example definitions are imported
(see [Persistence](#persistence--backup)). The front end expects the API here; CORS is open only for
`http://localhost:5173`.

### Smoke test

```bash
# liveness
curl http://localhost:5238/ping
# render a bundled declarative report to a PDF
curl -X POST http://localhost:5238/api/report/render-stored/hello -o hello.pdf
```

### Build & test

```bash
dotnet build        # whole solution (Buelo.slnx)
dotnet test         # xUnit suite
```

## Authoring a report

### Declarative (YAML)

Send the YAML definition + JSON data to `POST /api/report/render-declarative?format=pdf|excel`.

```yaml
kind: report
name: invoice
meta: { page: { size: A4, margin: "2cm" } }
header:
  - text: { value: "INVOICE #{{ data.number }}", style: { bold: true, size: 16 } }
  - divider: { color: "#1D9E75", thickness: 2 }
content:
  - table:
      data: data.items
      columns:
        - { width: 4*, header: "Product", cell: "{{ item.name }}" }
        - { width: 1*, header: "Qty",     cell: "{{ item.qty }}", align: right }
        - { width: 2*, header: "Total",   cell: "{{ currency(item.price * item.qty) }}", align: right }
      footer:
        - { span: 2, text: "Total", style: { bold: true, align: right } }
        - { text: "{{ currency(sum(data.items, 'price * qty')) }}", style: { bold: true, align: right } }
```

Blocks (`text`, `markdown`, `table`, `row`, `column`, `card`, `image`, `divider`, …), the `{{ }}`
expression language and its standard library, bands, grouping/aggregation, and the reusable modules
(`styles`/`component`/`theme`/`formats`/`lib`/`validator`) are documented in full in
[`docs/declarative-format.md`](docs/declarative-format.md).

### C# (`IDocument`)

Send the C# source to `POST /api/report/render`. The class is compiled with Roslyn and instantiated
with your data:

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
            page.Margin(2, Unit.Centimetre);
            page.Content().Text((string)_data.title);
        });
}
```

You can also **eject** a declarative report to an equivalent C# `IDocument`
(`POST /api/report/eject`) to graduate from YAML to code.

## API reference

| Controller | Base | Routes |
|---|---|---|
| `ReportController` | `api/report` | C#: `POST render`, `POST validate`, `POST render/{id}`, `POST preview/{id}`, `GET formats` · Declarative: `POST render-declarative`, `POST render-stored/{name}`, `POST eject` |
| `SchemasController` | `api/schemas` | `GET` (lists kinds), `GET {kind}` (JSON Schema of the kind) |
| `DeclarativeValidationController` | `api/validate-data` | `POST` (validate a value against a `kind: validator`) |
| `RenderHistoryController` | `api/render-history` | `GET` (render history) |
| `TemplatesController` | `api/templates` | CRUD + `{id}/artefacts[/{name}]`, `{id}/files`, `{id}/versions[/{n}[/restore]]` |
| `GlobalArtefactsController` | `api/artefacts` | CRUD + `GET by-name/{name}` |
| `WorkspaceController` | `api/workspace` | `GET tree`, `POST folders`, `POST/GET/PUT files[/content]`, `DELETE nodes`, `GET types` |
| `ValidateController` | `api/validate` | `POST` (one file), `POST project` |
| *(minimal)* | `/ping` | `GET` (public liveness; open even with the API key gate on) |

Render/preview responses are `application/pdf` (or the Excel content type); use `?format=` where
applicable. Errors: `404` (not found), `400` (bad/missing data or unresolved import).

## Persistence & backup

A self-hosted instance keeps **all durable content in a database**: declarative definitions, the
editor workspace, C# templates (with version history), global artefacts, and the render log. The
database is the source of truth.

**Provider** is chosen by config — `Buelo:Database:Provider`:

- `sqlite` (**default**) — a single file `buelo.db`, zero server. Back up by copying the file (when
  idle) or with continuous replication ([Litestream](https://litestream.io/)).
- `postgres` — point `Buelo:Database:ConnectionString` at a PostgreSQL server. Back up with
  `pg_dump`/PITR or your managed provider's snapshots.

**Schema** is applied at startup by `EnsureBueloDatabase()`: migrations are run when present
(SQLite migrations live in `Buelo.Persistence/Migrations/`, PostgreSQL in
`Buelo.Persistence.Postgres/Migrations/`), otherwise the schema is created from the model. Add or
update migrations after a model change:

```bash
# SQLite (default)
dotnet ef migrations add <Name> -p Buelo.Persistence -s Buelo.Persistence
# PostgreSQL
dotnet ef migrations add <Name> -p Buelo.Persistence.Postgres -s Buelo.Persistence.Postgres
```

**Seeding:** on first boot the bundled `definitions/` examples are imported into the database
(idempotent — a `_system/seeded` marker prevents re-import, so deleting an example doesn't bring it
back). After that the `definitions/` folder is just seed data.

## Configuration

Environment variables / `appsettings.json` under the `Buelo` section:

| Key | Default | Purpose |
|---|---|---|
| `Buelo:Database:Provider` | `sqlite` | `sqlite` or `postgres` |
| `Buelo:Database:ConnectionString` | `Data Source=buelo.db` | DB connection |
| `Buelo:ApiKey` | *(empty = auth off)* | Bearer key gating the API (opt-in; `/ping` stays open) |
| `Buelo:RenderTimeoutSeconds` | `30` | Per-render timeout (`0` disables) |
| `Buelo:DefinitionStorePath` | `definitions` | Folder seeded into the DB on first run |
| `Buelo:TemplateStorePath` | `templates` | Root for the opt-in file-system store |

CORS is fixed to `http://localhost:5173` (the editor). OpenAPI is exposed only in Development.

## Security model

Buelo is **single-tenant, self-hosted**: whoever holds the API key is trusted. The C# path compiles
and runs arbitrary code by design — that is the instance owner's own code on their own server, so
there is no sandbox. Don't expose an instance publicly without the API key gate enabled.

## License

QuestPDF runs under the Community license. See the QuestPDF licensing terms for usage limits.
