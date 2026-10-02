# Milestone 3: Objects, arrays, strings and dispatch

Status: in progress; bounded native object control flow, roots and concrete class inheritance are implemented. Updated 2026-10-02.

The first native class checkpoint now runs an independently authored small class
created, mutated and passed through calls, returning an Int32 result identical to .NET.
Managed references stay within the runtime. This milestone does not by itself
establish a collector, class-library compatibility or the stable embedding API.

## Proposed slices

| Slice | Outcome | Status |
| --- | --- | --- |
| 3a | Detached class identities, field storage and logical reference maps | Complete for bounded layout model |
| 3b-a | Concrete reference/null types and validated locals, joins, calls and returns | Complete for detached IR contract |
| 3b-b | CIL object translation, construction/field access and verified static/instance calls | Complete for bounded straight-line profile |
| 3c | Native object allocation, instance ownership and phase A root frames; first class differential execution | Complete for bounded straight-line profile |
| 3b-c | Reference-stack inference through branches/backedges | Complete for bounded ordinary-method control flow |
| 3c-a | Budgeted execution frames and native-stack regression checks | Complete for bounded object backend |
| 3c-b | Packed exports, arena lifetime diagnostics and constrained native call chains | Complete for bounded object backend |
| 3c-c | Opt-in execution microbenchmarks and helper-batching experiment | Complete; larger batches rejected |
| 3d | Inheritance, casts and verified virtual/interface dispatch | In progress |
| 3d-a | Bounded class hierarchies and detached ancestor assignability | Complete for layout/reference IR |
| 3d-b | Inherited native field storage and constructor chains | Complete for bounded concrete class profile |
| 3d-c | Class casts and runtime type tests | Planned |
| 3d-d | Verified virtual/interface dispatch | Planned |
| 3e | Int32/reference arrays, identity, mutation and ordered bounds/null failures | Planned |
| 3f | UTF-16 strings and an explicit bounded operation/literal profile | Planned |
| 3g | Integration review, composed example and supported-boundary consolidation | Planned |

These are reviewable boundaries, not schedule estimates. Split a slice when
correctness findings justify it. Preserve primitive regressions at every step.

## Slice 3a outcome

[ADR 0022](../decisions/0022-detached-object-layouts.md) introduces bounded type
snapshots and a detached `ObjectModel`. The compiler follows class fields through
explicit resolution and retains image-scoped TypeDef/FieldDef identity. Reference
slots are derived from field kinds; namesakes, forwarding and reference cycles
retain exact identities without retaining readers.

The initial classes are concrete, nongeneric, automatic-layout definitions
derived directly from the explicitly selected fieldless `System.Object` base.
Only Int32, named CLASS and primitive OBJECT field signatures are included.
Readonly/access flags remain visible. Layouts grant no field/method access.
Unsupported storage, type/module initialization and reference-assembly inputs
fail without partial models. This is a compiler API foundation: no object CLI
command, reference instruction translation or native object execution is claimed.

Review covers image/token identity, reader ownership, cycles, input/snapshot
budgets, field declaration order, exact closure and reference-slot classification.
It caught four boundaries: base decoding must not consume the last field
signature node; FieldDef uniqueness is per image across owners; and configurable
owner limits must not defeat bounded display-name formatting; the reserved module
TypeDef cannot describe an instance class. Checks and reduced
regressions cover these. Metadata failures retain field tokens and inclusion paths.

Final validation: zero build warnings/errors, all 71 focused object/type-input
cases pass, and the full local suite passes 687 tests in 18.4 seconds, including
the existing native Int32/UBSan corpus. Local main and cached origin/main remain
ancestors; the remote advertises no branch refs. No upstream integration, push,
hosted work or runtime service was started. No remaining issue was found within
this bounded layout contract; allocation/rooting execution is still untested.

## Slice 3b-a outcome

[ADR 0023](../decisions/0023-typed-reference-value-flow.md) adds concrete class
identity and null to shared IR values. `ReferenceIrProgram` validates value flow
against an explicit multi-root catalog, preserving the existing class/field
bounds. Null enters reference storage; classes widen to the selected object
base. Loads and call results retain exact types. Declared joins, locals,
arguments and returns reject integer/reference mixes, sibling assignments,
implicit downcasts and retagged values. Reference equality and truth branches
are permitted; reference arithmetic and ordered comparisons are rejected.

This is a detached compiler API contract, not a reference CIL translator. The
shared validator retains complete iterative method/block closure, earlier
operands, fault edges and aggregate limits. The primitive emitter rejects
reference/null IR before returning source. `IrType` changed from an enum to a
record in the early compiler API; the Int32 translation profile, emitted C++
and status ABI preserve their semantics.

Self-review covered type payloads/identity, both branch edges, callee result and
argument types, cycles, unused catalog roots and exact resource boundaries. A
forged null callee parameter could otherwise be dereferenced before its method
was validated; a guard and reduced regression now reject it as an API error.
Reference hash length is checked before catalog hashing. No remaining issue was
found within the structural contract; access, initialization, ownership and
execution are still unverified.

Final local validation: zero build warnings/errors; all 64 new reference IR cases
pass in the full suite of 751 tests (14.2 seconds), including native Int32
.NET comparisons and sanitizer checks. Local main and cached origin/main remain
ancestors; the remote advertises no branch refs. No push, hosted work or service
was started.

## Slice 3b-b outcome

[ADR 0024](../decisions/0024-straight-line-object-operations.md) adds the bounded
`ObjectTranslator` and `ObjectIrProgram`. Translation preserves exact constructor,
method and field identities, explicit receivers, void calls and ordered
allocation/field operations. Nonvirtual `callvirt` retains its receiver null
check; direct calls do not insert one. Constructors initialize through the
explicitly selected trivial Object constructor before other work. Access and
readonly receiver checks precede IR consumption. Fault edges remain explicit.

The profile is straight-line: branches/reference-stack inference are rejected
with diagnostics. Slice 3b-c will expand that after the first native class
checkpoint. Class catalogs now compose multiple roots, using cached type inputs
without weakening aggregate bounds. Bindings, initializer scans and snapshots
share a 65,536-unit metadata work ledger; repeated image/token bindings are cached.
An Int32 boundary keeps managed references internal. Native object emission,
allocation, ownership, roots and GC remain unimplemented.

Self-review found and fixed aggregate fields bypassing limits when their snapshots
were cached, and nongeneric nested methods bypassing enclosing generic rejection.
Candidate binding scans now consume shared work, and budgeted member formatting
checks its owner chain before recursive display formatting. Method inputs retain
raw declaration names and a hard 64-owner ceiling. Reduced tests also cover
readonly local aliases, allocation/void call copies at the exact aggregate limit,
wrong receivers/targets/bindings and every object-operation fault edge. No
remaining issue was found within this bounded translation contract.

Validation: zero build warnings/errors; 60 new object translation/conformance cases
pass in the full local suite of 811 tests (14.4 seconds). The authored C# class
probe agrees with .NET using a bounded test-only evaluator, including Int32
wrapping, readonly initialization, reference mutation and a null field fault.
Authored raw IL confirms direct null calls, nonvirtual callvirt null checks and
null identity comparison. This is translation evidence, not native object
execution. The existing native Int32/sanitizer corpus also passes. Toolchain:
SDK 10.0.400, .NET 10.0.11, existing macOS ARM64 native harness. Local main and
cached origin/main remain ancestors; the remote advertises no branch refs.
No push, hosted work, additional agent or runtime service was started.

## Slice 3c outcome

[ADR 0025](../decisions/0025-native-object-ownership-and-roots.md) adds separate
phase A root assignment and `ObjectCppEmitter`. Native target layouts provide
checked sizes, alignments and reference-field offsets. An explicit context links
frames to an instance-owned bounded heap, with a reserved synchronization word
in each header. Root new objects before constructor calls, root returned
references before later allocation, retain caller/callee arguments and locals,
and pop frames on every exit. Byte/count allocation limits and teardown are
explicit. References remain internal to an experimental Int32 status boundary.
The heap retains objects until teardown; no collector or persistent embedding API
is implemented. Limits are 1,024 roots per method, 64 active calls, 1 MiB of object
storage and 4,096 allocations per invocation. Reference control flow remains
unsupported before emission.

Review covered pointer interconversion/typed deletion, checked standard layout,
root publication and call results, ownership before dereference, zeroed storage,
all fault exits and boundary output lifetime. An identity comparison of a duplicated
value exposed tautological C++ output under warnings-as-errors; fold that already-
evaluated identity in the shared comparison helper. Object and primitive raw-IL
native regressions cover it. Root validity is checked before the private observer
can inspect pointers. No remaining issue was found within the retained-heap profile.

Validation: seven authored C# entries compare 1,463 .NET result/fault cases per
O0/O2 configuration through native helpers and the C boundary under ASan/UBSan,
with exceptions/RTTI disabled. Observers check references held by callers/callees,
reference returns, allocating constructors and children reachable through fields.
Native checks include foreign/invalid pointers and roots, null faults, exact IL
origins, zero fields, descriptors, byte/count limits, constructor allocation
failure, frame cleanup and destruction. Authored raw IL adds direct-null-call/
callvirt, null/same/distinct identity, integer self-comparison and a native-only
bounded recursion check. Root limits, forged IR and unsupported control flow
have compiler API cases. Final validation: zero build warnings/errors; all 818
local tests pass in 18.0 seconds, including the primitive native/UBSan corpus.
Toolchain: SDK 10.0.400, .NET 10.0.11, Apple Clang 17 on macOS ARM64. Local main
and cached origin/main remain ancestors; the remote advertises no branch refs.
No push, hosted work, additional agent or runtime service was started.

## Slice 3b-c outcome

[ADR 0026](../decisions/0026-reference-control-flow.md) extends ordinary object
methods through branches/backedges. A bounded work queue revisits stack inputs
until null/class/Object types converge. Incompatible kinds/depths and uses that
would narrow a widened Object fail without partial IR. Integer/object translation
share instruction partitioning and branch vocabulary. Cached call sites preserve
call-copy accounting across revisits, and field snapshots have token indexes.
An additional 262,144-unit closure budget bounds instruction/input/argument/edge
work independently of output size. Constructors remain straight-line; ordered
reference comparisons, argument stores, switches and handlers remain unsupported.

The native profile is now `object-control-flow-native-v1`. Root plans include
block inputs and `(block, value)` identities. Different input and temporary slots
preserve parallel edge transfers, including reference swaps on backedges. Native
block scopes follow publication of a method frame; loop allocation/arithmetic
faults pop it. Heap ownership, Int32 boundary and prior resource limits remain.
The heap still retains objects until teardown, with no collector/liveness claim.

Review checks convergence after backedge widening, final strict consumer validation,
aggregate budgets, root identities, scoped C++ jumps, parallel edge transfer and
pending-fault origins. Revisit work includes argument copies and avoids repeated
field scans. Reduced fixtures reject a callee that becomes invalid only after
widening and inference work that exceeds its bound while final IR would fit.
No remaining issue was found within this bounded retained-heap profile.

Seven authored C#/raw-IL entries compare 2,093 result/fault cases per native O0/O2
configuration under ASan/UBSan, through private helpers and the C boundary. They
cover linked-object traversal, nullable selection, identity, sibling-class-to-Object
widening, allocating stack swaps and arithmetic exits/origins. An independently
authored .NET DynamicMethod supplies the raw swap oracle. Native observers inspect
original references during loop allocation; forced allocation failure unwinds
frames. Compiler checks preserve constructor/ordering/argument-store rejections
and root limits. A near-limit regression verifies 65,504 call arguments remain
within the call-copy ledger when a loop is revisited.

Final local validation: zero build warnings/errors; all 839 tests pass in 26.9
seconds, including primitive and object native sanitizer cases. Toolchain: SDK
10.0.400, .NET 10.0.11, Apple Clang 17 on macOS ARM64. Local main and cached
origin/main remain ancestors; the remote advertises no branch refs. No push,
hosted work, additional agent or runtime service was started.

## Slice 3c-a outcome

[ADR 0027](../decisions/0027-budgeted-execution-frames.md) replaces native method
storage with target-sized typed frames in a separate, stable 1 MiB execution
arena. It retains depth 64 and the phase A root contract, adds resource status 9,
and releases frames/roots on all normal/fault paths. Internal argument packets
are budgeted; native bodies access frame slots through bounded non-inlined
helpers. Measurement caught megabyte-sized O0 spill frames even after explicit
locals moved to the heap, motivating the small dispatch loop between helpers.

Validation includes O0/O2 native finite/mutual recursion, exact/short budgets,
alignment, depth, many arguments, fault ordering, reuse after failure and roots.
The reduced wide case has 1,024 checked additions and 2,048 locals per invocation;
Clang stack reports give 272/208-byte maxima at O0/O2 without instrumentation.
ASan/UBSan execution also builds with a 2 KiB frame warning threshold. Existing
object differential probes continue to exercise reference returns and backedges.
These are local macOS ARM64 results, with no performance, collector or wider
platform claim. The primitive backend retains its previous storage model.

Final validation: zero build warnings/errors; all 416 targeted native, object,
C++ emission and Int32 tests pass in 39.6 seconds, including the O0/O2 sanitizer
and stack-report regressions. Review found no remaining issue within this
bounded profile. Local main and cached origin/main are ancestors; the remote
main query returned no branch ref. No push or hosted work was started. Follow-up
review identified the wide export and diagnostic gaps addressed by slice 3c-b.

## Slice 3c-b outcome

[ADR 0028](../decisions/0028-packed-boundaries-and-arena-diagnostics.md) changes
the experimental object export to a packed input pointer/count, avoiding a native
parameter list that grows with managed arity. Profile v2 uses a distinct symbol
and rejects invalid counts/null pointers before input reads or output writes.
The existing entry argument packet remains charged to the execution arena.

ASan builds now poison unused/released arena slots. Nested byte-sized storage,
live neighbors and zero-initialized reuse are checked, and a deliberate stale
read in a test-owned subprocess must produce `use-after-poison`. Reservation
alignment is identical in instrumented and ordinary builds. Root cleanup still
precedes storage release; this adds diagnostics without claiming collection.

Local Apple Clang 17 macOS ARM64 reports packed export maxima of 288/224 bytes
at O0/O2 for arities through 4,096. The wide finite/mutual-recursion test now runs
both ordinary and ASan/UBSan builds on a pthread requesting 256 KiB. The observed
268 KiB stack is checked against a 320 KiB ceiling. Static per-function maxima
for that entire translation unit, including its harness, are 1,520/768 bytes;
both builds retain the 2 KiB frame warning. Other targets and helper-dispatch
performance still require their own measurements.

Final validation: zero build warnings/errors and 420 targeted native, object,
C++ emission and Int32 tests passed in 26.9 seconds. Review checked ABI callers,
argument validation, reservation arithmetic and root/lifetime ordering; no
further defect was identified in this slice. No push or hosted work was started.

## Slice 3c-c outcome

[ADR 0029](../decisions/0029-measured-helper-batching.md) records a local baseline
and rejects a four-statement helper-batching candidate. An opt-in benchmark checks authored
arithmetic, loops and calls against .NET while separating fresh-boundary costs
from private arena reuse. It adds no automatic timed tests or hosted work.

The candidate's three-workload probe had 37 step helpers instead of 50 and an
8.2% smaller complete executable. However, reference-field native tests produced
2,304–2,576-byte frames at O0 with sanitizers, exceeding the 2 KiB ceiling. The
candidate was reverted; two-statement helpers remain. Timing samples and
unchanged comparators vary substantially, so no throughput improvement is claimed.
Reference-heavy workloads and other targets still need their own benchmarks.

Both benchmark runs matched all timed checksums to .NET. The candidate passed
419/420 targeted cases; its native frame-budget failure triggered the revert.
The restored baseline passes both focused O0/O2 frame-budget cases. Production
and test sources match the previously validated `c7a9147`; only the opt-in
harness and documentation are retained. Builds have zero warnings/errors.

## Slice 3d-a outcome

[ADR 0030](../decisions/0030-detached-class-hierarchies.md) extends the detached
layout catalog to include base classes while preserving declaration-local field
identities and reference slots. It rejects inheritance cycles, sealed/missing
bases and unsupported ancestors within the existing aggregate budgets. Concrete
derived references widen to ancestors in detached locals, arguments, returns
and declared joins; downcasts and sibling conversions remain invalid. A shared
ancestry cache bounds repeated assignment checks across the method closure.

Native object validation explicitly rejects inherited catalogs until base field
storage and constructor ordering are lowered. Abstract classes, casts, virtual
and interface dispatch remain later work. The frame arena, two-statement helper
baseline and resource limits are unchanged.

Validation: zero build warnings/errors; 504 targeted regression cases pass,
including native O0/O2 safety checks. Review removed eager diagnostic-name
allocation on valid ancestor steps; the added resource regression and all 145
focused hierarchy/model/reference cases then pass. All 25 assignability pairs
in the authored .NET hierarchy agree with the detached IR contract.

## Slice 3d-b outcome

[ADR 0031](../decisions/0031-native-inherited-fields-and-constructors.md) composes
ancestor fields into one standard-layout concrete allocation with a single
header. Descriptors supply base links, actual field offsets and inherited
reference maps; base methods retain the derived object's identity and dynamic
type. A separate 65,536-slot physical field budget bounds generated expansion.

Single-block constructors call their immediate base once on the same rooted
receiver. Declared-field initializers and independent base arguments may run
before that call, retaining C# fault order. Unfinished receiver reads/aliases/
escapes, skipped/repeated bases and inherited readonly writes fail validation.
Public/same-owner access and the trivial selected Object constructor remain the
bounded access/runtime contract.

Native O0/O2 probes cover three-level construction, hidden fields, base-typed
calls/returns, roots during allocating base bodies, zero fields, resource faults,
cleanup/reuse and cross-image equal-token field identities. Review preserves
typed C++ lifetime/addressing and moves diagnostics onto failure paths. Field
operations now occupy individual small helpers after new lookup code exposed
O0 sanitizer spill growth; the existing 2 KiB stack gate remains.

Final validation: zero build warnings/errors; all 526 targeted native/object/
reference-IR/C++/Int32 cases pass in 43.3 seconds. The final typed accessor tables
also pass their 4,096-field O0/O2 regressions and the 21 focused hierarchy cases.
Review and local main/cached-origin ancestry checks are complete. No push or
hosted execution was started.

## Next checkpoint and exit gates

Slice 3d-c next adds class casts and runtime type tests against these descriptor
base links. Verified dispatch follows that executable layout. Performance work from ADR 0029
is deferred and does not block these capability slices; any later optimization
must retain the O0/O2 native-stack and reference-operation gates.
The retained-heap execution checkpoint does not clear milestone 4: precise
collection, stress at every allocation and poisoned reclamation must prove that
these roots survive actual reclamation. Phase B and the wider ADR 0001/0002
experiments remain planned.

The Alchemy/CoreLib general-filter gate remains unresolved. No native checkpoint
clears the library audit, catch/filter/finally, generics, initialization, browser
or mobile execution gates.
