# Milestone 5: Values and managed semantics

Status: in progress. Recorded 2026-10-04.

The milestone covers structs, boxing, closed generics, delegates, exceptions and
type-test filters. These are separate capability gates; completion of a layout
contract does not establish executable support. Existing primitive and object
profiles continue to reject unsupported reachable semantics.

| Slice | Outcome | Status |
| --- | --- | --- |
| 5a | Detached value layouts, nested field paths and precise embedded-reference maps | Complete for detached layout planning |
| 5b | Bounded metadata discovery of value layouts | Complete for metadata-backed catalogs |
| 5c.1 | Value copies, field operations, calls/joins and expanded roots in typed IR | Complete for detached contracts |
| 5c.2 | Managed storage locations and lifetime checks | Planned |
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

## Slice 5b outcome

ADR 0056 connects value layouts to bounded metadata discovery with selected-runtime
identity, a shared metadata ledger and existing reference storage. Review checked
ownership, forwarding, signature kind mismatches, shared budgets and diagnostics.
All 128 selected local tests pass. Inline fields in classes, executable values,
boxing and other milestone 5 semantics remain subsequent work.


## Slice 5c.1 outcome

ADR 0057 adds exact inline value flow and embedded-reference root plans. Review
checked copies versus references, pure operations versus fault edges, field
identity, call/edge assignment, complete type closure and expanded resource bounds.
All 158 selected local checks pass, including the existing O0/O2 collection-stress
regressions after the shared verifier change. Managed locations remain unimplemented.
