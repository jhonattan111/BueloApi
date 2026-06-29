# WORKFLOWS.md — Buelo Backend

## 1. Agent execution workflow

1. Read `TASKS.md` and pick the next `[ ] pending` sprint, respecting the dependency chain
2. Read the chosen sprint file in `ai/sprints/`
3. Confirm that all dependencies listed in the `Dependencies` section are marked as `done`
4. Implement only the scope defined in the sprint — do not anticipate tasks from future sprints
5. Run `dotnet build` and `dotnet test` — both must pass with zero errors
6. Mark all the sprint tasks as `[x]` in the sprint file
7. Update the sprint status to `[x] done` in `ai/TASKS.md`

---

## 2. Sprint implementation workflow

### Standard order by layer

```
1. Buelo.Contracts   ← new models, interfaces, enums
2. Buelo.Engine      ← business logic, parsers, store
3. Buelo.Api         ← endpoints, controllers
4. Buelo.Tests       ← unit tests covering each layer
```

Always in this order — never reference `Buelo.Engine` from `Buelo.Contracts`.

---

## 3. Workflow for adding a new engine parser/component

1. Create the file in `Buelo.Engine/`
2. If it exposes new types: add the models/interfaces to `Buelo.Contracts/` first
3. Inject it into `TemplateEngine` via the constructor or a direct call (no internal DI in the engine)
4. Register it in the container in `EngineExtensions.cs` if needed
5. Cover it with tests in `Buelo.Tests/Engine/`

---

## 4. Workflow for adding a new endpoint

1. Define the request/response contract (records in `Buelo.Contracts` if reusable, or local records in the controller if exclusive)
2. Add the method to the controller with the correct route attribute
3. Call `TemplateEngine` or `ITemplateStore` via injection (primary constructor)
4. Return explicit types (`Ok(result)`, `NotFound()`, `BadRequest(msg)`) — don't return a generic `IActionResult` without need
5. Test in `Buelo.Tests/Api/` with at least: happy path, not found, bad input

---

## 5. Workflow for adding a new `ITemplateStore`

1. Create the class in `Buelo.Engine/` implementing `ITemplateStore`
2. Add a registration extension method in `EngineExtensions.cs` (e.g.: `AddBueloFileSystemStore()`)
3. Do NOT register it as the default — `InMemoryTemplateStore` is the default; the new impl is opt-in via `appsettings` or an explicit call in `Program.cs`
4. Cover it with round-trip tests in `Buelo.Tests/Engine/`

---

## 6. Workflow for deprecating a template mode

1. Add `[Obsolete("message")]` on the enum value in `Buelo.Contracts/TemplateMode.cs`
2. Do NOT remove the value — keep runtime support
3. Do NOT throw an exception when processing the obsolete mode — just run normally
4. Document it in the `## Template Modes` section of `SKILLS.md`
5. The frontend handles the deprecation visually — the backend only warns at compile time via `[Obsolete]`

---

## 7. Template validation workflow (/validate endpoint)

```
POST /api/report/validate
  body: { template, mode }
      ↓
  TemplateHeaderParser.Parse(source)
      ↓
  SectionsTemplateParser.Parse(stripped)
      ↓
  WrapSectionsTemplateAsync(...)
      ↓
  CSharpScript.Create(...).GetDiagnostics()   ← compile without executing
      ↓
  map line: diagnosticLine - wrapperLineOffset = userLine
      ↓
  return { valid, errors[] }    ← always HTTP 200
```

Never throw an unhandled exception in this endpoint — catch everything and return `valid: false`.

---

## 8. Versioning workflow in SaveAsync

```
SaveAsync(record)
  ↓
  read current version via GetAsync(record.Id)    ← if it exists
  ↓
  create TemplateVersion { Version = current.Version + 1, Template, Artefacts, SavedAt }
  ↓
  persist snapshot (memory dict / file versions/{n}.snapshot.json)
  ↓
  overwrite the main record
```

Don't version when `Id == Guid.Empty` (create) — version 1 is the initial record itself.
