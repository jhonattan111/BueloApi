# definitions/ — exemplos declarativos do Buelo

Mocks no **padrão declarativo** (YAML), lidos pelo `FileSystemDefinitionStore` no layout
`{kind}/{name}.yml`. Substituem os antigos templates C# (`templates/`, removidos).

## Layout

```
definitions/
  report/        report completos (renderáveis)    → hello, invoice, colaboradores
  component/     componentes reusáveis (use/slots)  → layoutPadrao
  styles/        classes de estilo (+ extends)      → corporativo
  theme/         page defaults + classes            → corporativo
  formats/       máscaras nomeadas                  → br
  lib/           expressões puras nomeadas          → vendas
  validator/     validadores (3 degraus)            → cpf
  data/          dados mock (JSON) p/ cada report    → hello/invoice/colaboradores
```

`report/*.yml` declaram seus `import:` (resolvidos pelo store); `data/` **não** é um kind — é só o
JSON de exemplo para alimentar cada report.

## Renderizar (API em `http://localhost:5238`)

```bash
# por nome (resolve os imports do store); o corpo é o JSON de data/<nome>.json
curl -X POST http://localhost:5238/api/report/render-stored/invoice \
  -H "Content-Type: application/json" \
  -d '{ "data": '"$(cat definitions/data/invoice.json)"' }' --output invoice.pdf

curl -X POST http://localhost:5238/api/report/render-stored/colaboradores \
  -H "Content-Type: application/json" \
  -d '{ "data": '"$(cat definitions/data/colaboradores.json)"' }' --output colaboradores.pdf
```

Também dá para renderizar YAML inline (`POST api/report/render-declarative`), ejetar para C#
(`POST api/report/eject`), pegar os schemas (`GET api/schemas/{kind}`) e validar valores
(`POST api/validate-data`).

## O que cada report exercita

- **hello** — markdown (heading/bold/itálico/lista) + numeração de página no rodapé.
- **invoice** — `use: layoutPadrao` (component + slot), `styles`/`theme`, tabela §5 com colunas
  `*`/`px`, `class`, pipe `| cnpj`, e footer com `sum(data.itens, 'preco * qtd')`.
- **colaboradores** — tabela com `groupBy: departamento`, header/subtotal por grupo.

> Os três são verificados por `Buelo.Tests/Engine/DeclarativeMocksTests` (renderiza do disco → PDF).
