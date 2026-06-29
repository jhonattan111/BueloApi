# Buelo declarative report format (AI-friendly reference)

This is a compact, complete reference for the **declarative YAML report format**. It is meant to
be read by an AI (or a human) to **generate valid reports**. A declarative report is YAML that the
engine lowers to a typed IR (`BueloDocument`) and renders with QuestPDF — **no C# involved**.

> Authoritative implementation: `Buelo.Engine/Declarative/DeclarativeAst.cs` (blocks/props) and
> `Buelo.Engine/Declarative/Expressions/` (the `{{ }}` language). JSON Schemas per kind are served at
> `GET /api/schemas/{kind}`.

## How a report is rendered

- The editor sends the YAML + a JSON **data** object to `POST /api/report/render-declarative?format=pdf|excel`.
- Inside the YAML, dynamic values come from `{{ ... }}` expressions evaluated against `data`.
- **Keep reports self-contained**: rendering from the editor does not pass external modules, so do
  **not** use `import:` / `use:` / `class:` (those need a styles/component module). Use inline
  `style: { ... }` instead.

## Top-level shape

```yaml
kind: report            # required; "report" is the only renderable kind
name: my-report         # required; an identifier
meta:
  page: { size: A4, margin: "2cm", orientation: Portrait }   # all optional
header:   [ <block>, ... ]   # optional band, repeated on every page
content:  [ <block>, ... ]   # the main band
footer:   [ <block>, ... ]   # optional band, repeated on every page
```

`meta.page.size`: `A4` | `A3` | `A5` | `Letter` | `Legal`. `orientation`: `Portrait` | `Landscape`.
`margin`: a CSS-like length string, e.g. `"2cm"`.

## Blocks

Each item in a band (or in a container's `content`/`items`) is a map with exactly **one** block key.

| Block | Shape | Notes |
|---|---|---|
| `text` | `{ value: "<expr text>", style: <Style> }` | Inline `{{ }}` allowed in `value`. |
| `markdown` | `markdown: \|` then an indented block | Rich text; supports `{{ }}`, `**bold**`, lists, `#` headings. |
| `html` | `html: \|` then an indented block | HTML subset. |
| `table` | see **Table** below | Data-oriented table. |
| `row` | `{ spacing: <num>, items: [ <block> ... ] }` | Horizontal layout. Each item may set `width`. |
| `column` | `{ spacing: <num>, content: [ <block> ... ] }` | Vertical layout. |
| `card` / `panel` | `{ style: <Style>, content: [ <block> ... ] }` | Boxed container (border/background/padding). |
| `image` | `{ url \| base64 \| source, fit, width, height }` | `source` = workspace artefact path. `fit`: width\|height\|area\|unproportional. |
| `spacer` | `spacer: <num>` | Vertical gap. |
| `divider` / `line` | `{ color: "#RRGGBB", thickness: <num> }` | Horizontal rule. |
| `pageBreak` | `pageBreak: true` | Forces a new page. |

### Table

```yaml
- table:
    data: data.items                 # expression yielding an array (no {{ }})
    groupBy: department              # optional: field name to group rows by
    rowStyle: { paddingY: 5, borderBottom: "1px #DDDDDD" }
    columns:
      - { width: 24px, header: "#",     cell: "{{ index + 1 }}" }
      - { width: 4*,   header: "Name",  cell: "{{ item.name }}" }
      - { width: 1*,   header: "Total", cell: "{{ currency(item.price * item.qty) }}", align: right }
    group:                            # only with groupBy
      header: { text: "{{ group.key }}", style: { bold: true, background: "#EEEEEE" } }
      footer: { text: "Subtotal: {{ currency(sum(group.items, 'price')) }}", style: { align: right, bold: true } }
    footer:                           # grand-total row (cells)
      - { span: 4, text: "Total", style: { bold: true, align: right } }
      - { text: "{{ currency(sum(data.items, 'price * qty')) }}", style: { bold: true, align: right } }
```

- Column `width`: `*` (fill), `3*` (relative weight), `120px`, `40%`, `2cm`. Default `*`.
- Per-row scope inside `cell`: `item` (the row object), `index` (0-based).
- Group scope inside `group.*`: `group.key` (the group value), `group.items` (rows in the group).

## Style object (`<Style>`)

Inline style used by `text`, `card`, columns, footer cells, group bands, `rowStyle`:

```yaml
style:
  color: "#222222"          # text color
  background: "#EEEEEE"      # fill
  bold: true
  italic: true
  size: 12                  # font size (pt)
  align: right              # left | center | right
  padding: 8                # box padding
  paddingY: 5               # vertical padding only
  border: "1px #CCCCCC"     # box border
  borderBottom: "1px #DDD"  # bottom border only
```

## Expressions: `{{ ... }}`

Evaluated against the JSON `data` object. Supports arithmetic (`+ - * / %`), comparison
(`== != < <= > >=`), logical (`&& || !`), ternary (`cond ? a : b`), null-coalescing (`a ?? b`),
function calls, and pipes (`value | fn`).

Scopes/vars:

| Name | Where | Meaning |
|---|---|---|
| `data` | everywhere | the JSON data object (e.g. `data.client.name`) |
| `item`, `index` | inside `table` cells | current row, 0-based index |
| `group.key`, `group.items` | inside `group.*` | group value, rows of the group |
| `now`, `today` | everywhere | current datetime / date |
| `page`, `pageCount` | bands | current page number / total pages |
| `report.name` | everywhere | the report's `name` |

### Standard library

Formatting/utility functions (call as `fn(x)` or pipe as `x | fn`):

`currency`, `date`, `cpf`, `cnpj`, `percent`, `upper`, `join`, `mask`, `if`, `coalesce`.

Aggregation over an array with a sub-expression evaluated per element:

`sum(arr, 'expr')`, `avg(arr, 'expr')`, `count(arr)`, `min(arr, 'expr')`, `max(arr, 'expr')`.

Example: `{{ currency(sum(data.items, 'price * qty')) }}`.

## Minimal complete examples

### Invoice (table + aggregation + currency)

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
footer:
  - text: { value: "Page {{ page }} of {{ pageCount }}", style: { align: center, size: 9 } }
```

Data: `{ "number": 1, "items": [ { "name": "A", "price": 10, "qty": 2 } ] }`

### Dashboard (cards in a row)

```yaml
kind: report
name: dashboard
content:
  - row:
      spacing: 8
      items:
        - card:
            style: { background: "#F1F5F9", padding: 10 }
            content:
              - text: { value: "Revenue", style: { size: 9, color: "#64748B" } }
              - text: { value: "{{ currency(data.revenue) }}", style: { bold: true, size: 16 } }
        - card:
            style: { background: "#F1F5F9", padding: 10 }
            content:
              - text: { value: "Profit", style: { size: 9, color: "#64748B" } }
              - text: { value: "{{ currency(data.revenue - data.expenses) }}", style: { bold: true, size: 16 } }
```

Data: `{ "revenue": 48200, "expenses": 31750 }`

## Generation checklist (for an AI)

1. Start with `kind: report` and a `name`.
2. Keep it **self-contained** — inline `style:`, no `import:`/`use:`/`class:`.
3. Reference data only through `{{ data.* }}`, `item`, `index`, `group.*`.
4. Use real stdlib names (`currency`, `cnpj`, `sum`, …) — they are fixed by the engine.
5. Make sure the YAML is valid and every `{{ }}` resolves against the provided data shape.
6. The matching data JSON is selected as the report's **Data source** in the editor.
