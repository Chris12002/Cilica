# Milestone 5: Values and managed semantics

Status: in progress. Recorded 2026-10-04.

The milestone covers structs, boxing, closed generics, delegates, exceptions and
type-test filters. These are separate capability gates; completion of a layout
contract does not establish executable support. Existing primitive and object
profiles continue to reject unsupported reachable semantics.

| Slice | Outcome | Status |
| --- | --- | --- |
| 5a | Detached value layouts, nested field paths and precise embedded-reference maps | Complete for detached layout planning |
| 5b | Bounded metadata discovery of value layouts | Planned |
| 5c | Value copies and managed storage locations in typed IR | Planned |
| 5d | Native struct operations, calls and collection stress | Planned |
| 5e | Boxing, unboxing and boxed identity/mutation | Planned |
| 5f | Bounded closed generic specialization and expansion diagnostics | Planned |
| 5g | Delegates, captured receivers and invocation | Planned |
| 5h | Managed exception objects, handlers, cleanup and type-test filters | Planned |
| 5i | Composed differential regressions and final review | Planned |

Split slices further when a separately testable semantic or ownership contract
requires it. Review each completed slice before committing. Keep resource limits
explicit and use focused local tests; hosted CI remains disabled.

Value layouts reuse content-scoped TypeDef/FieldDef identities. An embedded value
is distinct from an object reference: copying it copies its fields, including
reference identities. Layout planning must detect recursive inline containment
and bound expansion even when the declaration graph is small. Native byte offsets
remain a target-code-generation concern. Managed storage locations must retain
their heap owner, or a validated execution-frame lifetime, before addresses are
introduced into executable IR.

Exceptions follow ADR 0004's explicit propagation and staged filter contract.
General filters must remain diagnostics until full search-before-unwind ordering
exists. Neither a pending numeric fault nor a metadata-only filter classification
constitutes managed handler execution.


## Slice 5a outcome

ADR 0055 implements detached layout validation and copy/reference-path planning.
The slice review checked identity collisions, inline cycles, repeated/empty
embeddings, aggregate expansion/path budgets and existing executable rejection.
All 96 selected value/object-layout tests pass. This establishes no native struct
semantics; the next slice supplies bounded metadata discovery. No hosted work ran.
