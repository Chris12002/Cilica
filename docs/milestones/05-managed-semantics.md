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
| 5d.2b.1 | Native scalar location IR emission with allocating callees | Complete for authored IR |
| 5d.2b.2 | Mutable value expansion into scalar location IR | Complete for authored IR with scalar/void returns |
| 5d.2c.1 | Caller-owned native struct-return packets | Complete for authored IR |
| 5d.2c.2a | CIL value closure discovery | Complete for bounded metadata inputs |
| 5d.2c.2b | CIL value body translation | Planned |
| 5d.2c.3 | Allocating value source programs and composed live-value stress | Planned |
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

## Slice 5d.2b.1 outcome

ADR 0061 connects authored scalar location IR to the existing object emitter through
an explicit wrapper and emission entry point. Local/argument slots and inherited
heap fields can be lent to allocating direct static callees. Every carrier copy
publishes its owner; only lending frames register tokens. Readonly/invariant
parameters are checked before executing the body, and existing access, constructor,
dispatch, arena and fault contracts remain in force.

Review checked field visibility, exact reference predicates, readonly lending,
publication before allocation, by-value parameter mutation, borrowed-frame lifetime,
packet byte accounting and complete cleanup on faults. It also removed repeated
linear value-type lookups from frame planning. No blocking findings remain. All
1,453 selected non-native regressions and 32 selected native regressions pass.
O0/O2 ASan/UBSan probes match C# for normal and extreme Int32 inputs, repeat collection
before allocation, and verify cleanup after invalid/readonly carriers, allocation
failure and token exhaustion. Exact/excess owner-root and expanded-storage budgets
are covered. The 2 KiB native stack-warning gate remains enabled.

The next gate is mutable value expansion into this scalar target, followed by
CIL translation and struct returns. Boxing, generics, delegates and managed
exceptions remain planned. Milestone 5 is not complete. Local main remains an
ancestor; no push, hosted work, new service or additional agent ran.

## Slice 5d.2b.2 outcome

ADR 0062 expands mutable inline locals, by-value parameters and synchronous struct
borrows into the validated scalar target. Nested field paths, explicit readonly
views, whole snapshots, stores/init and void mutators reuse the existing layout
and native frame/root contracts. Empty values erase their storage while preserving
calls and their faults.

Review checked overlapping copies, whole replacement versus readonly field writes,
parameter isolation, owner/backing roots, output revalidation and bounded expansion
before creating carrier arrays. No blocking findings remain. All 171 distinct
selected checks pass: 76 focused checks including O0/O2 native differential and
borrow probes, 94 layout/metadata/value/native-planning checks, and one additional
empty-borrow call regression in the final 10-test lowering run. The 2 KiB sanitizer
stack gate remains enabled. Native probes preserve live reference identities and
reject a malformed final leaf before changing destination fields.

CIL translation, allocating value source programs, inline heap fields and struct
returns remain separate gates. Milestone 5 is still in progress. Local main remains
an ancestor; no push, hosted work, service or additional agent ran.

## Slice 5d.2c.1 outcome

ADR 0063 adds caller-owned struct-return packets through the location-lowering v2
profile. Result slots are typed, rooted caller locals; returning callees receive
checked output locations and return void after copying their snapshots. Nested
returns cannot expose a finished frame, failed calls skip result loads, and empty
results preserve calls without output storage.

Review checked hidden signature/argument remapping, distinct result buffers,
reference publication, full destination validation before body execution, ordered
fault propagation and all generated resource charges. No blocking findings remain.
All 83 selected value/location/native checks pass, including O0/O2 chained/nested
return comparisons, divide-by-zero/overflow origins, unchanged packed output on
failure and live embedded reference identity/reclamation. Final O0/O2 probes also
verify exact measured arena capacity succeeds and one byte less fails with complete
cleanup. The 2 KiB native stack-warning gate remains enabled.

CIL discovery/translation and composed allocating value source programs are next.
Inline heap values, boxing, generics, delegates and managed exceptions remain
planned. Milestone 5 is not complete. Local main remains an ancestor; no push,
hosted work, service or additional agent ran.

## Slice 5d.2c.2a outcome

ADR 0064 discovers static CIL closures with exact value/borrow signatures and
operand identities, shared metadata budgets and checked member visibility. Review
removed a quadratic scan and closed private-scope field access. All 83 selected
discovery/layout checks pass. This is a metadata checkpoint; it does not execute
CIL values or validate their evaluation stack. Body translation is the next slice.
Local main remains an ancestor. No hosted work ran.
