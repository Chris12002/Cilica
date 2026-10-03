# Milestone 3: Objects, arrays, strings and dispatch

Status: in progress; bounded native object control flow, roots, concrete class inheritance class/interface casts/type tests and bounded native vectors are implemented. Updated 2026-10-03.

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
| 3d-c | Class casts and runtime type tests | Complete for bounded concrete classes |
| 3d-c-a | Interface casts and runtime type tests | Complete for bounded nongeneric contracts |
| 3d-d | Verified virtual/interface dispatch | Complete for bounded class/interface dispatch profile |
| 3d-d-a | Exact class virtual slot/target resolution | Complete for bounded metadata profile |
| 3d-d-b | Closed-world class dispatch maps and native calls | Complete for bounded concrete class profile |
| 3d-d-c | Interface mapping and native interface calls | Complete for bounded nongeneric profile |
| 3d-d-c-a | Exact nongeneric interface method targets | Complete for bounded metadata profile |
| 3d-d-c-b | Interface catalogs, reference flow and detached maps | Complete for bounded detached preparation |
| 3d-d-c-b-a | Detached interface catalogs and reference flow | Complete for bounded detached profile |
| 3d-d-c-b-b | Bodyless interface declarations and validated implementation plans | Complete for bounded detached preparation |
| 3d-d-c-c | Native interface calls and execution conformance | Complete for bounded retained-heap profile |
| 3d-d-c-c-a | Interface call translation and fixed-point body discovery | Complete for bounded CIL/IR profile |
| 3d-d-c-c-b | Typed native interface lookup and execution conformance | Complete for bounded retained-heap profile |
| 3d-d-d | Mixed class/interface calls on validated interface-mapping owners | Complete for bounded retained-heap profile |
| 3d-e | Unique common class/interface inference at stack joins | Complete for bounded CIL/retained-heap profile |
| 3e | Int32/reference arrays, identity, mutation and ordered bounds/null failures | Complete for bounded vector profile |
| 3e-a | Detached vector identities, covariance, operations and root contracts | Complete for detached validation |
| 3e-b | SZARRAY metadata/signatures and bounded array CIL translation | Complete for translation |
| 3e-c | Native variable storage, checked stores and execution conformance | Complete for bounded retained-heap profile |
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

## Slice 3d-c outcome

[ADR 0032](../decisions/0032-class-casts-and-type-tests.md) adds named-class
`castclass` and `isinst` through exact descriptor ancestry. Null stays null;
successful operations preserve reference identity. Failed casts report status
7 with the original IL location, while failed tests return null. Target-only
types enter the bounded catalog. Ownership checks precede descriptor reads;
results use existing rooted arena slots. Unfinished constructor receivers cannot
escape through casts/tests before base initialization.

Two authored entries compare 84 .NET/native outcomes per O0/O2 build, with
sanitizers and the current native-frame gate. Tests cover identity, allocation
roots, fault order/origins, exact/short budgets, cleanup/reuse, malformed and
unsupported targets, forged IR and same-name/equal-token cross-image types.
Review is complete; all 544 relevant local regressions pass in 31.9 seconds,
with zero build warnings/errors. No hosted work or push was started.

## Next checkpoint and exit gates

Slice 3d-d-a now supplies exact class virtual target selection
([ADR 0033](../decisions/0033-exact-class-virtual-targets.md)). It distinguishes
ordinary overrides from new virtual slots, preserves canonical slot identity,
rejects final overrides and unsupported remapping, and uses a shared bounded
metadata ledger. The authored .NET oracle agrees on 41 valid receiver/declaration
pairs. This operation resolves metadata only; executable class dispatch is a
separate compiler/runtime contract below.
Review is complete, with zero build warnings/errors and all 114 focused
dispatch/member-binding regressions passing. No hosted work was started.

Slice 3d-d-b now wires those choices into fixed-point method closure, detached
dispatch-map validation and native calls
([ADR 0034](../decisions/0034-native-class-virtual-dispatch.md)). Maps cover every
compatible concrete catalog receiver and validate nearest implementations,
canonical slots and final overrides. Direct virtual calls retain direct body
semantics. Per-declaration typed descriptor lookup finishes before invocation;
its selected pointer lives in the caller's arena frame, avoiding another live
native wrapper frame. Override bodies may add new receiver types and call sites.

Four authored entries compare 172 .NET/native outcomes and exact fault origins
per O0/O2 configuration. Coverage includes overloads, property getters, newslot
hiding, nonvirtual hiding, sealed/inherited overrides, reference and void returns,
argument-before-null ordering and receiver discovery inside an allocating override.
Native probes check roots, every allocation failure stage, exact/short frame
budgets, ownership, equal-token cross-image dispatch, direct null calls and
recursion within a checked 320 KiB pthread stack ceiling under ASan/UBSan with
static Clang stack reports and the 2 KiB frame gate.
Unsupported override bodies retain their virtual inclusion path in build errors.
Distinct-class evaluation-stack joins still conservatively widen to Object;
the fixtures use declared base locals with separate branch stores.

Review is complete. Builds have zero warnings/errors; all 604 relevant local
native/object/reference-IR/C++/Int32/exact-target regressions pass in 55.1 seconds.
Both O0/O2 native dispatch cases also pass after the final stack-report and
thread-size assertions. Local main and cached origin/main remain ancestors;
no push or hosted work was started.

Slice 3d-d-c-a adds exact interface method mapping
([ADR 0035](../decisions/0035-exact-interface-method-targets.md)). It preserves
inherited mappings through hiding, supports reimplementation and validated
explicit interface MethodImpl bodies, then reuses exact class-slot selection.
An authored .NET oracle agrees on 47 receiver/member pairs. Iterative diamond
closure and shared metadata work remain bounded; malformed maps, cycles,
duplicate declarations and exact/excess budgets have reduced regressions.
This is metadata resolution only. Interface catalogs/reference flow/maps and
native calls are separate slices 3d-d-c-b/c; abstract classes, generics,
covariance, default bodies and class MethodImpl remapping remain unsupported.
Review is complete, with zero build warnings/errors and all 186 targeted
metadata/compiler regressions passing after the final queue-bound change.
The wider 188-case run also passes both O0/O2 native class-dispatch checks,
including sanitizers and stack reports. Local main/cached origin/main ancestry
checks pass; no push or hosted work was started.

Slice 3d-d-c-b-a supplies detached interface catalogs and reference flow
([ADR 0036](../decisions/0036-interface-catalogs-and-reference-flow.md)). Separate
contract records have no instance layout. Declared class edges, interface parents
and reference fields share existing type/metadata budgets. Iterative graph checks
and assignment closure preserve diamonds, inherited implementations and exact
cross-image identities. Locals, direct-call arguments, returns and declared joins
accept implemented/parent interfaces and null; loads retain their declared type.
At this checkpoint object operations, roots and native emission remained gated;
the subsequent 3d-d-c-b-b/3d-d-c-c slices below now supply declarations/maps and
native calls. Interface casts/tests still require later lowering.

Review is complete. Builds have zero warnings/errors; all 698 relevant local
native/object/reference-IR/type-input/C++/Int32/exact-target checks pass in 40.5
seconds, including existing O0/O2 sanitizer, root/resource and stack regressions.
Local main and cached origin/main remain ancestors; no hosted work was started.

Slice 3d-d-c-b-b supplies bodyless declarations and detached dispatch plans
([ADR 0037](../decisions/0037-detached-interface-dispatch-plans.md)). A bounded
declaration reader shares provenance logic with body input without weakening
its IL requirement. Plans retain initial mappings separately from effective
overrides and share catalog, method, signature and metadata budgets. Structural
validation covers every compatible receiver, inherited mappings through hiding,
reimplementation, private final slots, exact signatures and nearest included
overrides. The authored .NET oracle agrees on 47 map/slot choices. These plans
do not read/translate body instructions, grant executable access or lower calls.
Call translation and fixed-point body discovery are supplied by the slice below;
native calls and their root/fault/stack conformance remain separate exit gates.
Performance tuning remains deferred.

Review is complete. Builds have zero warnings/errors; all 738 relevant local
native/object/reference-IR/declaration/type-input/C++/Int32/exact-target checks
pass in 43.0 seconds, including existing O0/O2 native regressions. Local main and
cached origin/main remain ancestors; no push or hosted work was started.

Slice 3d-d-c-c-a integrates interface call translation and executable body closure
([ADR 0038](../decisions/0038-interface-call-translation.md)). Bodyless signatures
stay in the plan; selected class bodies enter ordinary translation. Mapping and
slot facts need no executable IL when unused. Cached exact choices support
fixed-point discovery as implementations introduce calls and receiver types.
Executable validation checks plan/catalog/signature/binding agreement and exact
call/map usage, including private explicit bodies without public access changes.
Class/interface targets and executable/declaration/fact identities share their
existing budgets. Null-only calls permit empty maps and retain argument faults
before their receiver check. Phase A assigns interface reference roots and call
safepoints, with its existing cap. Authored .NET/translated-IR comparisons cover
the inheritance/mapping cases, recursion, fields and reference/void results.
Slice 3d-d-c-c-b supplies native lookup, execution, resource/fault cleanup and
root checks during retained-heap allocations. Class dispatch on MethodImpl
owners, interface casts/tests and inferred interface joins remain explicit gaps.

Review is complete. Builds have zero warnings/errors. All 771 broader local
native/object/reference-IR/declaration/type-input/C++/Int32/exact-target checks
pass in 37.1 seconds, including existing O0/O2 native regressions. The final
50 focused interface-translation/type-input checks also pass, including two
additional constructor-before/after-base regressions added during review.
Local main and cached origin/main remain ancestors; no push or hosted work was
started.

Slice 3d-d-c-c-b supplies native interface membership and typed body lookup
([ADR 0039](../decisions/0039-native-interface-dispatch.md)). Contract references
retain their original pointer and dynamic class descriptor without a layout or
wrapper. Ownership checks precede descriptor inspection; exact membership/table
addresses preserve inherited/parent contracts and cross-image identities. Class
and interface calls share typed body pointers, arena packets, depth/frame checks
and cleanup. Eleven authored C# entries compare 170 native/.NET result and
fault-origin cases per O0/O2 configuration. Native diagnostics check roots and
interface fields during later allocations, exact/short frame budgets, every
object-count failure stage, cleanup/reuse and invalid/foreign/wrong-type/null
receivers. A raw two-image case retains distinct private bodies for same-name,
equal-token contracts. Constrained-thread recursion and mutual recursion retain
the existing 64-call policy. Collection/reclamation, interface casts/tests,
inferred joins and ordinary class virtual resolution on MethodImpl owners remain
explicit gaps; the broader 3d integration gate stays open.

Review is complete. Builds have zero warnings/errors; all 779 relevant local
native/object/reference-IR/declaration/type-input/C++/Int32/exact-target checks
pass in 40.6 seconds. ASan/UBSan and the 2 KiB frame warning pass at O0/O2;
uninstrumented stack reports for the interface driver/generated code show maxima
of 896 bytes at O0 and 848 bytes at O2. An initial O0 failure was confined to the
test driver's large `main`; splitting diagnostic cases retained the same gate.
Local main and cached origin/main remain ancestors; no push or hosted work ran.


Slice 3d-c-a adds named public nongeneric interface casts and type tests
([ADR 0040](../decisions/0040-interface-casts-and-type-tests.md)). Casts reuse exact
membership tables and preserve object identity, including inherited and parent/
diamond contracts. Null remains null; mismatches report status 7 for casts or
return null for type tests. Ownership precedes descriptor reads, and unknown
descriptor addresses are compared without dereferencing them. Rooted arena
storage, constructor restrictions, fault order/origin and cleanup are unchanged.
Target-only contracts require no interface body or dispatch plan.

Three authored entries compare 144 .NET/native outcomes at each of O0/O2,
including composed class/interface casts, explicit implementations, identity and
cast/argument/null fault order. Allocation observers check both cast and type-test
roots; exact/short budgets, every object-count failure stage, cleanup/reuse and
invalid/foreign/null references pass. Copied/unreadable descriptors cannot gain
membership. Raw native fixtures distinguish same-name/equal-token contracts
across images and test null against empty target-only tables. Malformed targets,
forged IR and casts/tests on unfinished constructor receivers retain regressions.

Review is complete: all 788 relevant local checks pass in 47.6 seconds, with zero
build warnings/errors. ASan/UBSan and the 2 KiB native-frame warning pass at O0/O2;
static stack reports measure maxima of 704 bytes at O0 and 640 bytes at O2 across
the new driver/generated code. Local main and cached origin/main remain ancestors.
No push or hosted work ran. Mixed class/interface calls are integrated by slice
3d-d-d below, and unique common-type joins by 3d-e. Collection/reclamation remains
a separate gate.

Slice 3d-d-d permits ordinary class virtual calls on hierarchies containing
validated interface-only MethodImpl rows
([ADR 0041](../decisions/0041-mixed-class-interface-dispatch.md)). Shared bounded
validation checks all interface mappings before class-slot selection. Class slots,
explicit interface bodies, inherited mappings and newslot chains remain distinct;
class MethodImpl remapping remains unsupported. Class-only closures need no
interface dispatch plan and do not translate unused mapped IL. The existing
translator fixed point and native calling convention need no changes.

Review completed with a warning-free solution build and 315 focused local tests
passing. Seventeen exact class target/slot comparisons match .NET; the new native
harness compares 184 outcomes per optimization level at O0/O2, including mixed
discovery order, roots across allocating calls, fault origins and cleanup. ASan/UBSan
and the existing 2 KiB static native-frame gate pass. Local main remains an ancestor;
no push or hosted work ran.

Slice 3d-e adds unique common class/interface inference at branch joins and
backedges ([ADR 0042](../decisions/0042-reference-join-inference.md)). A bounded
refinement pass uses the complete catalog after method discovery, retaining
common ancestor sets through stack forwarding until convergence. Only then does
it choose a single most-specific type and revalidate every consumer/edge.
Ambiguous intersections require explicitly typed locals or casts; Object values
are never narrowed. Native storage, dispatch and phase A roots remain unchanged.

Review completed with a warning-free solution build and 824 selected local
regressions passing in 64.5 seconds. The new harness compares 210 .NET/native
outcomes at each of O0/O2, including a raw stack-carrying loop, exact fault origins,
inferred input roots across allocations and resource-failure cleanup. ASan/UBSan
and the existing 2 KiB static native-frame gate pass. Local main remains an ancestor;
no push or hosted work ran.

Slice 3e-a establishes the detached vector and operation contract
([ADR 0043](../decisions/0043-detached-array-contracts.md)). Structural identities
retain the selected Object anchor and Int32 or nominal-reference element, sharing
the existing type limit with classes/interfaces. Reference covariance, fields,
declared joins, calls and returns retain exact identities. Array allocation/length/
load/store operations require typed operands and fault edges; phase A includes
array references and allocation safepoints. Constructor escape checks also cover
array stores. This slice established no CIL/native execution claim; subsequent 3e-b/3e-c supply it.

Review completed with a warning-free solution build, an 874-check broader local
pass and a 275-check final focused pass after signature-validation hardening.
The new detached suite compares all 196 modeled assignment pairs against .NET and
checks deep malformed types, exact/excess type limits, covariance, fault edges,
constructor escapes and roots. Local main remains an ancestor; no push or hosted
work ran.

Slice 3e-b implements SZARRAY field/signature closure and bounded CIL translation
([ADR 0044](../decisions/0044-array-metadata-and-cil.md)). A shared signature grammar
covers executable methods, class/interface dispatch and model fields. `newarr`,
Int32/reference element operations and the same-block `ldlen; conv.i4` idiom lower
to the existing array IR with fault edges and allocation roots. Exact/null joins
work; element access after unequal-reference joins requires an explicit array local.
Catalog registration excludes transient null-only views from stack inference.
The native gate remained until the subsequent 3e-c checkpoint.

Review completed with a warning-free solution build, 910 broader local checks and
156 final focused checks passing. The new suite includes 36 array translation cases,
with authored .NET comparisons and a reduced cross-image runtime-identity regression.
Local main remains an ancestor; no push, hosted work or additional agents ran.

Slice 3e-c implements native vector storage and checked element operations
([ADR 0045](../decisions/0045-native-array-storage.md)). Native v8 owns a fixed header
and typed variable buffer, charging both to the retained heap. Exact descriptor
membership covers nominal/interface types and array covariance; null/bounds/type
checks precede stores, and failed stores preserve their element. Dynamic reference
callbacks expose slots for inspection/future tracing. Array values and loaded
references use phase A roots across class/array allocations and loops. New bounds
and array-type-mismatch fault tags retain precise origins and boundary cleanup.

Review completed with a warning-free solution build and 919 selected local checks
passing. The array harness compares 155 .NET/native outcomes at each of O0/O2 under
ASan/UBSan; both static frame reports have a 656-byte maximum for the generated code
and driver. Local main remains an ancestor; no push, hosted runs or agents ran.

The next slice is bounded UTF-16 strings and explicit literal/operation semantics
(3f), followed by the composed example/review (3g). Collection remains a separate
milestone.

Performance work from
ADR 0029 is deferred until representative applications run and does not block
capability work. The 2 KiB warning is a conservative tested baseline, not a
permanent production requirement. Any revised threshold needs measured speed
gains and target-specific whole-chain stack budgets, with sanitizer overhead
considered separately; ownership, rooting and fault regressions remain required.
The retained-heap execution checkpoint does not clear milestone 4: precise
collection, stress at every allocation and poisoned reclamation must prove that
these roots survive actual reclamation. Phase B and the wider ADR 0001/0002
experiments remain planned.

The Alchemy/CoreLib general-filter gate remains unresolved. No native checkpoint
clears the library audit, catch/filter/finally, generics, initialization, browser
or mobile execution gates.
