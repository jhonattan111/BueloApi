# Blocks

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

## Table

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

Precedence when combined (see [`modules.md`](modules.md) for `class:` / `kind: styles`):
**inline `style:` > `class:` > theme**.
