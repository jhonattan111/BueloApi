# Modules — `component`, `styles`, `theme`, `formats`, `lib`, `validator`

Every declarative file declares a `kind` at the top. `report` is the only renderable one (see
[`report.md`](report.md)); the rest are **importable modules** — reusable pieces a report pulls in.

| `kind` | Role | Model (`Buelo.Engine/Declarative/Modules/ModuleAst.cs`) |
|---|---|---|
| `component` | Reusable layout fragment (params + slots), used via `use:` | `ComponentModule` |
| `styles` | Named style classes, `extends:` for inheritance | `StylesModule` |
| `theme` | Bundle of styles + page defaults + color palette | `ThemeModule` |
| `formats` | Named masks/formats | `FormatsModule` |
| `lib` | Named pure expressions (calculated fields) | `LibModule` |
| `validator` | Declarative validation rules, 3 escalating tiers | `ValidatorModule` |

**Name resolution:** the **template-local** artifact is tried first, then the **global registry**. A
collision is a compilation error naming the origin of each definition. Indexed by
`Declarative/Modules/ModuleRegistry`.

**Versioning:** `use: defaultLayout@2` is an optional, advisory pin — `StripPin` removes the `@version`
suffix before lookup; it does not validate that the version exists. No pin resolves to latest.

## Importing modules into a report

```yaml
import:
  - component: letterhead
  - styles: corporate
use: letterhead
with:
  title: "Account Statement"
```

`import:` lists the modules the report needs (by `kind: name`); `use:`/`with:` wraps the report in a
component, passing its params. The editor gathers the workspace's
`*.{styles,component,theme,formats,lib,validator}.yml` files and sends them in the render request's
`Modules` field so these resolve — see [`README.md`](README.md#declarative-yaml) for the request shape.
A self-contained report that doesn't need reuse can skip `import:`/`use:` entirely and inline
`style: { ... }` on its blocks instead.

## `kind: component` — params + slots

Composition-based reuse: explicit `params`, one or more named `slots`, and a `body` — a **flat list of
blocks** (the same vocabulary as a report band) that may contain a `{ slot: <name> }` marker where the
caller's content is injected.

```yaml
kind: component
name: letterhead
params:
  title:   { type: string }
  company: { type: string, default: "Buelo Accounting" }
slots: [content]
body:
  - row:
      items:
        - column:
            content:
              - text: { value: "{{ company }}", style: { bold: true, size: 16, color: "#1D9E75" } }
              - text: { value: "{{ title }}", style: { size: 11, color: "#666666" } }
        - column:
            content:
              - text: { value: "Issued: {{ today }}", style: { size: 9, color: "#999999", align: right } }
  - divider: { color: "#1D9E75", thickness: 2 }
  - spacer: 10
  - slot: content
```

The report above (`use: letterhead`, `with: { title: ... }`) has its own `content:` band injected at
the `{ slot: content }` marker.

- **Scope:** explicit `params` + minimal ambient context (`now`, `page`, `pageCount`). A component does
  **not** see the report's `data` unless it's passed in via `with:` — that hygiene is what makes reuse
  safe across reports with unrelated data shapes.
- **Slots:** one or more named injection points (`slots: [content]` above); a report fills the default
  slot via its own `content:`, or `slots: { name: [...] }` for a multi-slot component.
- **Nesting:** components can `use:` other components.

## `kind: validator` — the extensibility ladder

Three escalating tiers, cheapest/safest first (`ValidatorModule` in `ModuleAst.cs`):

```yaml
kind: validator
name: cpf
# Tier 1 — declarative (format + common checksum)
format: "###.###.###-##"
rules:
  - { digits: 11 }
  - { checksum: { scheme: mod11, weights: [10,9,8,7,6,5,4,3,2] } }
```

```yaml
kind: validator
name: steuerId
# Tier 2 — pure expression (fits in a reduce; the value binds to the first param)
expr: "{{ len(digits(id)) == 11 && checkDigit(digits(id)) == last(digits(id)) }}"
params: [id]
```

```yaml
kind: validator
name: steuerIdComplex
# Tier 3 — reference to a registered C# extension (self-hosted only)
ref: GermanHelpers.ValidateSteuerId
```

Tiers 1-2 are safe to share in the global registry; tier 3 (code) only enters via the local
registry/extension mechanism. Validate a value against one via `POST /api/validate-data`, or reference
one from a column/binding with `validate: cpf`.

## `kind: styles` / `kind: theme` / `kind: formats` / `kind: lib`

- **`styles`** — named `<Style>` classes (same shape as the inline `style:` object, see
  [`blocks.md`](blocks.md#style-object-style)), referenced from a block via `class: title`. A class can
  `extends:` another class in the same module for inheritance.
- **`theme`** — bundles a `page` default, a `styles` map, and a color `palette` under one importable
  name.
- **`formats`** — named masks/formats (e.g. locale-specific number/date masks) referenced by name.
- **`lib`** — named pure expressions for calculated fields, reused across reports:

  ```yaml
  kind: lib
  name: sales
  expr:
    finalPrice: "{{ price * (1 - discount) }}"
    marginPct:  "{{ (revenue - cost) / revenue * 100 }}"
  ```

  Usage: `{{ sales.finalPrice }}` (with `item`/`data` in scope).
