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
| 5d.2c.2b | CIL value body translation | Complete for the bounded static compiler-library profile |
| 5d.2c.3a | Values composed with allocating vector IR | Complete for authored IR |
| 5d.2c.3b | CIL value/vector allocation and composed live-reference stress | Complete for Int32 vector CIL |
| 5d.2c.3c.1 | Value/class-construction IR composition | Complete for authored IR |
| 5d.2c.3c.2 | CIL value/class construction and live-reference stress | Complete for the bounded compiler-library profile |
| 5e.1 | Named-struct boxing, exact unbox copies/borrows and payload/root contracts | Complete for authored IR |
| 5e.2a | Native boxed storage, identity/mutation, fault status and collection | Complete for private runtime helpers |
| 5e.2b | Boxed IR scalar expansion, native emission and fault provenance | Complete for authored IR |
| 5e.3 | Bounded CIL boxing/unboxing and composed differential regressions | Complete for named nongeneric structs |
| 5f.1 | Closed generic static method specialization and expansion diagnostics | Complete for unconstrained methods on nongeneric owners in value programs |
| 5f.2 | Closed generic struct layouts and member binding | Complete for unconstrained closed structs and static members |
| 5f.3 | Primitive-only closures and bounded generic constraint validation | Complete for class/struct/public default-constructor constraints |
| 5f.4 | Closed generic reference layouts and construction | Complete for concrete classes with scalar/reference fields and exact constructors |
| 5g.1 | Delegate identities, captures, invocation and root contracts | Complete for authored single-target Int32/void IR |
| 5g.2 | Native delegate storage, invocation and collection | Next |
| 5g.3 | CIL delegate binding and composed differential probes | Planned |
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

## Slice 5d.2c.2b outcome

ADR 0065 translates bounded static CIL values into the existing copy/borrow IR
and native lowering. Local mutation, nested borrows, overlapping copies, exact
stack joins, loops and struct returns now have source-level translation evidence.
Review fixed hidden-type use through operands/signatures and made the loop oracle
observe its computation. No blocking findings remain.

All 174 selected discovery/layout/value/borrow verifier checks and 14 selected
location/return-lowering checks pass. Eight local O0/O2 native configurations pass
with ASan/UBSan and the 2 KiB stack gate; only the two branch configurations were
repeated after strengthening that fixture. These are 196 distinct selected checks.
The compiler build has zero warnings/errors. Local main remains an ancestor.
No push, hosted work, new service or additional agent ran.

Milestone 5 remains in progress. The next gate is allocating value source programs
and composed live-reference stress (5d.2c.3); boxing, generics, delegates and
managed exceptions/type-test filters remain planned. The new CIL API is currently
a compiler-library entry point; existing CLI profiles are unchanged.

## Slice 5d.2c.3a outcome

ADR 0066 adds opt-in value/vector IR composition, shared operation validation and
allocation safepoints with embedded roots. Scalar lowering preserves vector
operands and rooted return packets. Manual review found no blocking issues;
all 104 selected value/location/composition checks pass. CIL allocation and native
live-reference stress remain the next slice. No hosted work ran.

## Slice 5d.2c.3b outcome

ADR 0067 connects bounded Int32 array allocation/access to CIL struct programs.
The selected runtime element identity, exact array types and paired length
conversion are checked. Collection-before-every-allocation probes preserve
embedded identity through nested copies, borrowed replacement and struct returns.
A sole callee-parameter root transfers into caller-owned return storage, and
clearing the final root permits reclamation. Exact/insufficient heap and arena
budgets, nested allocation failure, fault provenance and context reuse pass.

Manual review found no blocking issues. This slice passed 254 distinct targeted
checks (49 initial CIL checks plus 217 shared/native checks with 12 overlapping).
Together with the preceding 104-check IR slice, 358 distinct checks passed. Native
O0/O2 ASan/UBSan probes retain the 2 KiB stack gate; their only repeat added the
nested allocation failure/recovery assertion. Local main remains an ancestor.
The compiler builds without warnings/errors; no push, hosted work, service or
additional agent ran.

Milestone 5 remains in progress. Class allocation/constructors composed with
values are next (5d.2c.3c), followed by boxing, generics, delegates and managed
exceptions. Existing CLI entry points remain unchanged.

## Slice 5d.2c.3c.1 outcome

ADR 0068 composes static value methods with class construction and fields through
shared constructor/access verification. Source allocation roots and scalar operand
mapping preserve return packets and constructor receivers. All 80 selected local
checks pass; manual review found no blocking issues. CIL class construction and
live object-reference stress remain next. No hosted work ran.

## Slice 5d.2c.3c.2 outcome

ADR 0069 connects class construction and inherited scalar/reference fields to
static CIL struct programs. Existing source/target verification enforces access,
readonly receiver rules and base initialization. Discovery shares bounded metadata
snapshots with value/reference catalog construction.

Review found that a struct-returning call in a constructor could require hidden
borrowed storage before base initialization. This gate now rejects value/borrow
flow throughout constructor bodies, with a reduced regression. A cached-snapshot
regression also verifies that combined field limits cannot be bypassed. No blocking
findings remain.

The final focused suite passed 256 checks, following 66 initial CIL checks and two
O0/O2 native configurations; these groups overlap. Native ASan/UBSan probes retain
six-object graphs through struct copies, heap-field borrows and sole-root returns,
including allocation inside base/derived constructors. They verify arithmetic/fault
parity, final reclamation, exact/insufficient heap/arena limits and nested allocation
failure with context reuse. The 2 KiB stack gate remains enabled.

Milestone 5 remains in progress. Boxing/unboxing is next (5e), followed by generics,
delegates and managed exceptions. Existing CLI entry points remain unchanged.
Local main remains an ancestor. No push, hosted work, service or additional agent ran.

## Slice 5e.1 outcome

ADR 0070 adds an opt-in authored boxing contract on the existing composed value
flow. It distinguishes fresh box allocation, exact unbox copies and mutable
payload borrows. Logical payload plans reuse bounded struct paths and embedded
reference maps; phase A roots include every box and borrowed owner, including
empty payloads. Earlier profiles and native lowering still reject boxing.

Review checked exact payload identities, required fault exits, copied-reference
roots, borrowed-owner roots, readonly projection and existing escape/resource
limits. It added regressions for identical empty layouts with distinct TypeDef or
image identities. No blocking findings remain. All 231 selected local verifier,
layout and root-planning checks pass; the compiler builds without warnings/errors.
Local main remains an ancestor. Native storage/execution and CIL boxing remain
the next gates; milestone 5 is not complete. No push, hosted work, service or
additional agent ran.

## Slice 5e.2a outcome

ADR 0071 adds typed boxed-payload descriptors and checked private helpers to the
existing allocator, tracing and location runtime. Boxes retain exact value/image
identity, support full copies and mutable borrows, and enter the Object reference
predicate. Whole copies validate the last reference before exposing any writes.
Combined native class/box slot limits are enforced before source expansion.

Review checked descriptor authentication, C++ member provenance, failure atomicity,
allocation accounting and owner/source roots. It strengthened the catalog mismatch
fixture to use two valid catalogs and added empty borrowed-owner collection stress.
No blocking findings remain. Local O0/O2 sanitizer probes pass with the 2 KiB stack
gate, including copy/mutation comparisons with .NET, cyclic graph tracing, invalid
inputs, exact/insufficient heap capacity, object-count recovery and pending-fault
preservation during collection callbacks.

All 78 selected local checks pass, including six O0/O2 native configurations.
The tightened catalog mismatch check also passed separately; no native probes
were repeated for that assertion-only change. The compiler builds cleanly.

This is a helper checkpoint. Boxed IR expansion/emission and generated fault
provenance are next (5e.2b), followed by CIL boxing. Milestone 5 remains in progress.
Local main remains an ancestor. No push, hosted work, service or additional agent ran.

## Slice 5e.2b outcome

ADR 0072 connects authored boxing IR to native execution through exact scalar
packets, checked owners and typed payload locations. Empty unboxes retain their
fault checks and owner roots across allocating synchronous calls. Expanded roots,
storage and arguments are revalidated; packets use the execution arena.

Review added a regression for hidden empty-owner root growth and checked profile
isolation, fault provenance and publication across allocation. No blocking findings
remain. All 143 selected verifier/lowering checks and two O0/O2 ASan/UBSan native
configurations pass (145 distinct checks). Native evidence covers .NET copy/mutation
parity, embedded references, empty borrows, null/type faults, unchanged failure
output, allocation recovery and exact/insufficient arena capacity. The 2 KiB stack
gate remains enabled and the compiler builds cleanly.

CIL boxing is next (5e.3); milestone 5 remains in progress. Local main remains an
ancestor. No push, hosted work, service or additional agent ran.

## Slice 5e.3 outcome

ADR 0073 translates named-struct box/unbox/copy CIL through the shared checked
pipeline. Real C# graph/copy/return programs agree with .NET under allocation-time
collection, and reduced `unbox` CIL mutates a payload through an allocating helper.
Null/type and allocation failures retain original method/IL locations, preserve
packed output and allow clean context reuse.

Review fixed the catalog's rejection of the standard C# empty-struct one-byte size
record, with reduced guards against broader sized/packed layouts. No blocking
findings remain. All 113 selected discovery/translation/metadata/native checks pass,
including O0/O2 ASan/UBSan with the 2 KiB stack gate. The compiler builds cleanly.

Closed generics are next (5f). This boxing gate covers named nongeneric structs;
primitive/enum/nullable boxing, ValueType views and boxed interfaces remain outside
the profile. Milestone 5 is in progress. No push, hosted work, service or additional
agent ran.

## Slice 5f.1 outcome

ADR 0074 specializes unconstrained static generic methods through the existing
value/boxing pipeline. Distinct instances retain original metadata provenance and
separate native symbols. Closed caller environments bind nested calls; finite
recursion deduplicates while growing signature trees fail with inclusion chains.

Review checked identity, substitution, resource bounds, rooted generic parameters
and fault propagation. All 52 selected local checks passed, including O0/O2 native
ASan/UBSan differential probes and the 2 KiB stack gate. Generic type layouts and
constraint validation are next. Milestone 5 remains in progress; no hosted work ran.

## Slice 5f.2 outcome

ADR 0075 adds closed struct layouts, field identities, nested scalar/root expansion,
static methods on closed owners and exact boxed specialization checks. Review
preserved old-profile diagnostics and added hidden-argument and recursive/expanding
layout regressions. All 105 selected local checks pass, including O0/O2 native
ASan/UBSan differential collection/fault probes with the 2 KiB stack gate.

Primitive-only closures and bounded constraint validation are next. Generic class
allocation and dispatch remain outside the current profile. Milestone 5 remains
in progress; no hosted work ran.

## Slice 5f.3 outcome

ADR 0076 admits scalar/reference-only closures without inventing inline layouts.
Metadata validation enforces the common special constraints, including nested unused
arguments, selected Nullable exclusion and public constructor declarations. Review
checked authority, malformed flags and exact metadata accounting. All 136 distinct
selected local checks pass across the final suite and focused review rerun, including
O0/O2 native scalar/constrained probes with ASan/UBSan and the 2 KiB stack gate.

Closed generic reference layouts and construction are next; broader constraints
and dispatch remain diagnostic. Milestone 5 remains in progress. No hosted work ran.

## Slice 5f.4 outcome

ADR 0077 specializes concrete class layouts, base classes, fields and constructors
through the existing object runtime. Review checked exact owners, readonly/base
initialization, cyclic root maps, limits and diagnostics. The initial 43 and
shared/native 116 checks passed; the final review selection passed 114 checks,
followed by five inclusion-path checks (overlapping selections).

O0/O2 ASan/UBSan probes compose generic class graphs with struct returns, boxes and
borrows, compare arithmetic/faults to .NET and verify allocation failure recovery.
The 2 KiB stack gate remains enabled. The bounded generic gate is complete;
generic dispatch, broader constraints and inline struct fields in classes remain
explicitly unsupported. Delegates are next (5g). Milestone 5 remains in progress;
no hosted work ran.

## Slice 5g.1 outcome

ADR 0078 defines exact single-target delegate identities, checked static/instance
captures and Int32/void invocation through the existing value flow. Review checked
access, closure reachability, fault exits, signatures and captured/delegate roots.
All 248 selected local verifier/catalog checks pass, including malformed targets
and the root budget. Earlier native profiles reject this source-only contract.
Native storage and invocation are next (5g.2). Milestone 5 remains in progress;
no hosted work ran.
