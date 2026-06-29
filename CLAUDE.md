# CLAUDE.md — BueloApi

Guia para agentes de IA (Claude Code) trabalhando neste repositório. É o **documento canônico** da arquitetura atual — em caso de divergência com docs antigos (`ARCHITECTURE.md`, `docs/`), este arquivo vence.

## O que é

`BueloApi` é a **API de geração de relatórios** do produto Buelo. Tem **dois caminhos de autoria**:

1. **C# (`IDocument`)** — recebe código C# em runtime, compila com **Roslyn**, instancia uma classe `QuestPDF.Infrastructure.IDocument` e retorna **PDF** (ou **Excel** via ClosedXML). É o "escape hatch" de poder total.
2. **Declarativo (YAML)** — recebe uma definição declarativa, faz *lowering* para um IR tipado (`BueloDocument`) e compõe via QuestPDF — **sem Roslyn**. É o caminho principal de autoria. Ver [`docs/blueprint-schema-canonico.md`](../docs/blueprint-schema-canonico.md) e a seção **Engine declarativo** abaixo.

> Faz parte do produto Buelo, junto do front [`BueloWeb`](../BueloWeb). O repo guarda-chuva é [`Buelo`](..) (submodules). O front consome esta API em `http://localhost:5238`.

## Stack

- **ASP.NET Core 10** (`net10.0`), C# com primary constructors
- **QuestPDF** (Community license) — layout PDF
- **Microsoft.CodeAnalysis.CSharp** (Roslyn) — compilação de template em runtime
- **ClosedXML** — saída Excel
- Testes: **xUnit** em `Buelo.Tests`

## Estrutura da solução (`Buelo.slnx`)

```
Buelo.Contracts   ← interfaces, models, enums. SEM lógica de negócio, SEM QuestPDF, SEM Roslyn
Buelo.Engine      ← compilação Roslyn, render (PDF/Excel), stores, validators
Buelo.Api         ← controllers ASP.NET Core + Program.cs
Buelo.Tests       ← xUnit (Engine/ e Api/)
```

**Direção de dependência (nunca inverter):** `Buelo.Api → Buelo.Engine → Buelo.Contracts`

## Comandos

```bash
dotnet build                          # da raiz do repo (usa Buelo.slnx)
dotnet test                           # roda Buelo.Tests
dotnet run --project Buelo.Api        # sobe a API em http://localhost:5238
```

**Após qualquer mudança, `dotnet build` e `dotnet test` devem passar com zero erros antes de considerar a tarefa concluída.**

## Conceito central: templates são classes C# `IDocument`

Um template é uma classe C# completa e compilável implementando `QuestPDF.Infrastructure.IDocument`. O construtor recebe os dados (e opcionalmente `PageSettings`):

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

`TemplateMode` tem **apenas** `FullClass = 1`. A antiga DSL `.buelo` e os modos `Sections`/`Builder`/`Partial` e a interface `IReport` **foram removidos** — não reintroduza.

## Pipeline de render (`TemplateEngine`)

`RenderAsync(template, data, mode = FullClass, pageSettings?)`:

1. `ConvertToDynamic(data)` — `object` → `ExpandoObject` via System.Text.Json
2. `CompileTemplate(source)` — `CSharpCompilation` (Release, nullable enable) → emite assembly em memória; erros viram `InvalidOperationException` com linha
3. `FindDocumentType` — primeira classe não-abstrata que implementa `IDocument`
4. `CreateDocumentInstance` — escolhe o ctor com **mais parâmetros**; parâmetros do tipo `PageSettings` recebem as settings, os demais recebem os dados (`ExpandoObject` direto, ou round-trip JSON para um modelo tipado)
5. `document.GeneratePdf()` → `byte[]`

`RenderTemplateAsync(record, data?, pageSettings?)`: usa `record.MockData` se `data` for null; `MergeSettings` = `request ?? template ?? Default`.

`ValidateAsync` compila e retorna `ValidationResult { Valid, Errors[] }` (linha/coluna) **sem** executar — nunca lança.

## Engine declarativo (YAML → IR → QuestPDF)

Pipeline: **YAML → `DeclarativeParser` (YamlDotNet) → `DeclarativeInterpreter`/`DeclarativeLowering` (avalia `{{ }}`, resolve diretivas e estilos) → `BueloDocument` (IR tipado) → `BueloDocumentRenderer` (compõe QuestPDF) → PDF.** Código em `Buelo.Engine/Declarative/`, `Buelo.Engine/Ir/`, `Buelo.Engine/Renderers/BueloDocumentRenderer.cs`. Orquestrador: `DeclarativeReportEngine` (singleton).

- **Kinds** (`kind:` no topo do YAML): `report` (renderável), `component` (params + slots + `use`/`with`), `styles` (classes + `extends`), `theme`, `formats` (máscaras), `lib` (expressões nomeadas), `validator` (3 degraus). Módulos indexados por `Declarative/Modules/ModuleRegistry`.
- **Blocos de layout** (`Declarative/DeclarativeAst.cs`): `text`, `markdown`, `table` (data-oriented, `groupBy`, footer com agregação), `row`, `column`, `card`/`panel`, `image`, `spacer`, `divider`, `pageBreak`; bandas `header`/`content`/`footer`; context vars `now`/`today`/`page`/`pageCount`/`report.name`.
- **Expressões `{{ }}`** (`Declarative/Expressions/`): lexer + parser recursivo + avaliador. Aritmética, comparação, lógico, ternário, `??`, pipes, chamadas; stdlib (`moeda`/`data`/`cpf`/`cnpj`/`percent`/`upper`/`join`/`mask`/`if`/`coalesce`…) + agregação `sum`/`avg`/`count`/`min`/`max`.
- **Persistência das definições:** `IDefinitionStore` (`{kind}/{name}.yml`) — `FileSystemDefinitionStore` (default, git-friendly) e `InMemoryDefinitionStore` (testes). Raiz via config `Buelo:DefinitionStorePath` (fallback `definitions`).
- **Operacional (EF Core):** `BueloDbContext` + `IRenderLog` (histórico de render). SQLite (dev) / Postgres (prod) por `Buelo:Database:Provider`. `AddBueloPersistence(config)` + `EnsureBueloDatabase()` no startup. Default sem DB = `NullRenderLog`.
- **Eject:** `CSharpEjector` gera um `IDocument` C# a partir do IR (graduação declarativo→código).
- **Exemplos:** `Buelo.Api/definitions/` (reports `hello`/`invoice`/`colaboradores` + módulos + dados mock em `data/`). Ver `Buelo.Api/definitions/README.md`.

## Superfície da API (rotas reais)

| Controller | Base | Rotas |
|---|---|---|
| `ReportController` | `api/report` | C#: `POST render`, `POST validate`, `POST render/{id}`, `POST preview/{id}`, `GET formats` · Declarativo: `POST render-declarative`, `POST render-stored/{name}`, `POST eject` |
| `SchemasController` | `api/schemas` | `GET` (lista kinds), `GET {kind}` (JSON Schema do kind) |
| `DeclarativeValidationController` | `api/validate-data` | `POST` (valida um valor contra um `kind: validator`) |
| `RenderHistoryController` | `api/render-history` | `GET` (histórico de render do EF store) |
| `TemplatesController` | `api/templates` | CRUD + `{id}/artefacts[/{name}]`, `{id}/files`, `{id}/versions[/{n}[/restore]]` |
| `GlobalArtefactsController` | `api/artefacts` | CRUD + `GET by-name/{name}` |
| `WorkspaceController` | `api/workspace` | `GET tree`, `POST folders`, `POST/GET/PUT files[/content]`, `DELETE nodes`, `GET types` |
| `ValidateController` | `api/validate` | `POST` (1 arquivo), `POST project` |
| *(minimal)* | `/ping` | `GET` (liveness público; aberto mesmo com API key) |

Render/preview retornam `application/pdf` (ou Excel). Use `?format=` quando aplicável. Erros: `404` (id não encontrado), `400` (sem dados).

## Contracts principais (`Buelo.Contracts`)

- **`TemplateRecord`** — `Id, Name, Description, Template (C#), Mode, DataSchema, MockData, DefaultFileName, OutputFormat (Pdf|Excel), PageSettings, CreatedAt, UpdatedAt, Artefacts[]`
- **`TemplateArtefact`** — `Path?, Name, Extension, Content` (arquivos anexos: mock data, schema, helpers `.cs`)
- **`ReportRequest`** — `Template, FileName, Data, Mode, PageSettings?`
- **`PageSettings`** — tamanho, margens (cm), cores, watermark, fonte, header/footer. `PageSettings.Default()` = A4 / 2cm
- **`IHelperRegistry`** — `FormatCurrency`, `FormatDate` (default: `DefaultHelperRegistry`)
- Stores: `ITemplateStore`, `IGlobalArtefactStore`, `IWorkspaceStore`, `IWorkspaceFileEnumerator`

## Registro no DI (`AddBueloEngine`)

`builder.Services.AddBueloEngine();` registra como singletons: `TemplateEngine`, `ITemplateStore → InMemoryTemplateStore`, `IGlobalArtefactStore → InMemory...`, `IWorkspaceStore/Enumerator → FileSystem...`, validators (`Json`, `Csharp`) + `FileValidatorRegistry`, renderers (`Pdf`, `Excel`) + `OutputRendererRegistry`, `IHelperRegistry → DefaultHelperRegistry`.

- Usa `TryAdd` → registre sua própria `IHelperRegistry`/`ITemplateStore` **antes** para sobrescrever.
- Persistência em disco: `AddBueloFileSystemStore()` (opt-in); raiz via `appsettings` `Buelo:TemplateStorePath` (fallback: `./templates`).

## Convenções

- **Camadas em ordem:** mudança nasce em `Contracts` → `Engine` → `Api`. Nunca referenciar `Engine` a partir de `Contracts`.
- Controllers usam **primary constructor injection**; retornem tipos explícitos (`Ok`, `NotFound`, `BadRequest`).
- `ITemplateStore` é **async** em tudo (`Task<T>`), mesmo a impl in-memory.
- Todo novo endpoint precisa de teste em `Buelo.Tests/Api/` (happy path + not found + bad input). Componentes de engine: teste em `Buelo.Tests/Engine/`.
- `Program.cs`: `AddBueloEngine()` + `AddBueloPersistence(config)`; CORS p/ `http://localhost:5173`; OpenAPI só em Development; licença QuestPDF Community; `EnsureBueloDatabase()`; `/ping` público; `ApiKeyMiddleware` (gate opt-in).
- **Config (env/appsettings):** `Buelo:ApiKey` (Bearer opt-in; vazio = auth off), `Buelo:RenderTimeoutSeconds` (default 30; 0 desliga), `Buelo:DefinitionStorePath` (default `definitions`), `Buelo:Database:Provider` (`sqlite`|`postgres`) + `Buelo:Database:ConnectionString`.
- **Cache de assembly Roslyn** por hash de conteúdo no `TemplateEngine` (renders repetidos do mesmo template C# pulam recompilação).
- **Modelo self-hosted:** sem sandbox/multi-tenant; quem tem a API key é confiável (ver blueprint).

## Histórico e referência

`docs/` guarda o histórico de sprints e guias detalhados (era DSL `.buelo` → era C#/QuestPDF). É **referência histórica**, não estado atual. `ARCHITECTURE.md` na raiz tem o racional do redesign.
