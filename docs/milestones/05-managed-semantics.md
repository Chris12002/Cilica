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
| 5c.2 | Managed storage locations and lifetime checks | Complete for bounded detached synchronous borrows |
| 5d.1 | Pure value copies lowered to the existing backend | Complete for authored IR, scalar returns and local native probes |
| 5d.2a | Private native scalar location carriers and rooted-storage stress | Complete for runtime helpers |
| 5d.2b | Native location IR emission and value expansion | Planned |
| 5d.2c | CIL value translation, native returns and composed live-value stress | Planned |
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


## Slice 5d.1 outcome

ADR 0058 lowers immutable value copies to the existing scalar/reference backend.
This slice can precede managed locations because it exposes no mutable address.
Review checked copy independence, nested ranges, parallel backedges, typed default
references and expanded work/argument limits. Three authored IR/native probes
match C# at O0/O2 with ASan/UBSan; all 98 selected value checks pass. The probes
allocate no managed objects and do not prove collection of live embedded roots.

Milestone 5 is **not complete**. At this checkpoint, the next implementation gate
was 5c.2: managed locations and lifetime checks, followed by CIL value translation
and native return packets/live-reference stress. Boxing, generic specialization, delegates and managed
exceptions/type-test filters remain unimplemented. All completed slices were
reviewed before local commits. Local main remains an ancestor; no push, hosted
work, new service or additional agent ran.


## Slice 5c.2 outcome

ADR 0059 defines managed locations with checked storage origins, invariant element
types and readonly propagation. Frame locals/parameters and heap fields can be
borrowed by direct calls, with nested inline fields and void mutators supported.
No location escapes through returns, locals/fields or joins; the entry cannot
accept an unowned borrow. Loads copy values and mutation requires writable storage
and fault edges. Root plans distinguish heap owners from interior storage and
charge both carrier slots through existing limits.

Native carriers, frame lifetime authentication, CIL value translation, struct return
packets and collection stress through live mutable values remain the next 5d.2 gate.
This detached slice does not execute byrefs or broaden existing native claims.
Array/boxed locations, byref locals/joins and ref returns require further contracts.

Review checked provenance restrictions, exact pointee identity, readonly lending,
explicit fault edges, void-call reachability and owner/backing-root separation.
All 972 selected local regressions passed, including O0/O2 native value copies
and collection stress. All 47 final focused location checks pass (overlapping the
broader run), including malformed deep types and exact/excess root/carrier budgets. The solution build has zero warnings
and errors. Local main remains an ancestor; no push, hosted work, new service or
additional agent ran. Native location execution remains explicitly gated.

## Slice 5d.2a outcome

ADR 0060 supplies private checked scalar carriers for active frame slots and owned
heap fields. Frame tokens cannot revive on address reuse; exact field metadata
enforces storage kind, readonly status and reference compatibility. Reference
stores remain visible to precise collection through the original rooted storage.

Review checked lifetime retirement, ownership-before-dereference, inherited field
indices, failed-write atomicity, unchanged outputs/faults and trusted native scope
boundaries. The initial O0 sanitizer stack warning was fixed by separating frame
and heap validation helpers, retaining the 2 KiB gate. All 24 selected native tests
pass, including O0/O2 carrier probes, collection/tracing, arrays/strings, inherited
fields, arena/boundary budgets and pure value copies. Carrier probes include exact
64-frame, 1,024-reference-slot and 65,536-integer-slot limits, collection stress and
terminal token exhaustion. Local main remains an ancestor. No hosted work ran.

This is a runtime-helper checkpoint. Managed-location IR emission, expanded mutable
values, CIL translation and struct-return packets remain the next gates; milestone
5 is still in progress.
