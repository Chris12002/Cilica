# Milestone 3: Objects, arrays, strings and dispatch

Status: in progress; straight-line native objects, instance ownership and phase A roots are implemented. Updated 2026-10-02.

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
| 3b-c | Reference-stack inference through branches/backedges | Planned after first native checkpoint |
| 3d | Inheritance, casts and verified virtual/interface dispatch | Planned |
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

## Next checkpoint and exit gates

Slice 3b-c now adds reference-stack inference through branches/backedges, preserving
parallel edge transfers and phase A root publication. The retained-heap execution
checkpoint does not clear milestone 4: precise collection, stress at every
allocation and poisoned reclamation must prove that these roots survive actual
reclamation. Phase B and the wider ADR 0001/0002 experiments remain planned.

The Alchemy/CoreLib general-filter gate remains unresolved. No native checkpoint
clears the library audit, catch/filter/finally, generics, initialization, browser
or mobile execution gates.
