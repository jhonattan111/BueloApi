# definitions/ — Buelo declarative examples

Mocks in the **declarative standard** (YAML), read by the `FileSystemDefinitionStore` in the
`{kind}/{name}.yml` layout. They replace the old C# templates (`templates/`, removed).

## Layout

```
definitions/
  report/        complete reports (renderable)        → hello, invoice, colaboradores
  component/     reusable components (use/slots)       → layoutPadrao
  styles/        style classes (+ extends)             → corporativo
  theme/         page defaults + classes               → corporativo
  formats/       named masks                           → br
  lib/           named pure expressions                → vendas
  validator/     validators (3 tiers)                  → cpf
  data/          mock data (JSON) for each report      → hello/invoice/colaboradores
```

`report/*.yml` declare their `import:` (resolved by the store); `data/` is **not** a kind — it is just the
example JSON to feed each report.

## Render (API at `http://localhost:5238`)

```bash
# by name (resolves the store's imports); the body is the JSON from data/<name>.json
curl -X POST http://localhost:5238/api/report/render-stored/invoice \
  -H "Content-Type: application/json" \
  -d '{ "data": '"$(cat definitions/data/invoice.json)"' }' --output invoice.pdf

curl -X POST http://localhost:5238/api/report/render-stored/colaboradores \
  -H "Content-Type: application/json" \
  -d '{ "data": '"$(cat definitions/data/colaboradores.json)"' }' --output colaboradores.pdf
```

You can also render inline YAML (`POST api/report/render-declarative`), eject to C#
(`POST api/report/eject`), fetch the schemas (`GET api/schemas/{kind}`), and validate values
(`POST api/validate-data`).

## What each report exercises

- **hello** — markdown (heading/bold/italic/list) + page numbering in the footer.
- **invoice** — `use: layoutPadrao` (component + slot), `styles`/`theme`, §5 table with `*`/`px`
  columns, `class`, the `| cnpj` pipe, and a footer with `sum(data.itens, 'preco * qtd')`.
- **colaboradores** — table with `groupBy: departamento`, header/subtotal per group.

> All three are verified by `Buelo.Tests/Engine/DeclarativeMocksTests` (renders from disk → PDF).
