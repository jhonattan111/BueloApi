# Expressions: `{{ ... }}`

Evaluated against the JSON `data` object. Deliberately simple: **pure, single-line, deterministic,
side-effect-free**. There is no imperative `for`/`if`, function definition, or state — flow control
lives in block-level constructs (`table.groupBy`, per-row `cell`), not in the expression language.
Real algorithms belong in the C# path (the escape hatch), not here — that boundary is what keeps the
YAML from becoming a bad programming language.

Supports arithmetic (`+ - * / %`), comparison (`== != < <= > >=`), logical (`&& || !`), ternary
(`cond ? a : b`), null-coalescing (`a ?? b`), function calls, and pipes (`value | fn`).

## Scopes / context variables

| Name | Where | Meaning |
|---|---|---|
| `data` | everywhere | the JSON data object (e.g. `data.client.name`) |
| `item`, `index` | inside `table` cells | current row, 0-based index |
| `group.key`, `group.items` | inside `group.*` | group value, rows of the group |
| `now`, `today` | everywhere | current datetime / date |
| `page`, `pageCount` | bands | current page number / total pages |
| `report.name` | everywhere | the report's `name` |

## Standard library

Formatting/utility functions (call as `fn(x)` or pipe as `x | fn`):

`currency`, `date`, `cpf`, `cnpj`, `percent`, `upper`, `join`, `mask`, `if`, `coalesce`.

Aggregation over an array with a sub-expression evaluated per element:

`sum(arr, 'expr')`, `avg(arr, 'expr')`, `count(arr)`, `min(arr, 'expr')`, `max(arr, 'expr')`.

Example: `{{ currency(sum(data.items, 'price * qty')) }}`.

## Pipes (sugar)

`{{ data.cnpj | cnpj }}` is equivalent to `{{ cnpj(data.cnpj) }}`, and pipes chain:
`{{ data.valor | currency }}`.

## Reusable named expressions (`kind: lib`)

For expressions used in more than one place, define them once and reference by name — see
[`modules.md`](modules.md#kind-lib).

Authoritative implementation: `Buelo.Engine/Declarative/Expressions/` (lexer, recursive-descent
parser, evaluator, stdlib registry). To add a new stdlib function, see
[`../workflows.md`](../workflows.md).
