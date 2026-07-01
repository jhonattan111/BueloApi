# `kind: report` — top-level shape

The only renderable kind. Sent (with a JSON `data` object) to
`POST /api/report/render-declarative?format=pdf|excel`.

```yaml
kind: report            # required; "report" is the only renderable kind
name: my-report         # required; an identifier
meta:
  page: { size: A4, margin: "2cm", orientation: Portrait }   # all optional
header:   [ <block>, ... ]   # optional band, repeated on every page
content:  [ <block>, ... ]   # the main band
footer:   [ <block>, ... ]   # optional band, repeated on every page
```

- `meta.page.size`: `A4` | `A3` | `A5` | `Letter` | `Legal`.
- `meta.page.orientation`: `Portrait` | `Landscape`.
- `meta.page.margin`: a CSS-like length string, e.g. `"2cm"`.

> This `meta.page` shape is intentionally simpler than the C# path's `PageSettings` (see
> [`page-settings.md`](page-settings.md)) — no watermark/colors/font here yet. Blocks and their
> content go in `header`/`content`/`footer`: see [`blocks.md`](blocks.md). Dynamic values use
> `{{ }}` expressions: see [`expressions.md`](expressions.md). Reusable layouts/styles/validators
> are pulled in via `import:`/`use:`: see [`modules.md`](modules.md).

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
2. For a standalone report, keep it **self-contained** — inline `style:`, no `import:`/`use:`/`class:`.
   Use modules only when the matching `*.<kind>.yml` files exist in the workspace.
3. Reference data only through `{{ data.* }}`, `item`, `index`, `group.*`.
4. Use real stdlib names (`currency`, `cnpj`, `sum`, …) — they are fixed by the engine, see
   [`expressions.md`](expressions.md).
5. Make sure the YAML is valid and every `{{ }}` resolves against the provided data shape.
6. The matching data JSON is selected as the report's **Data source** in the editor.
