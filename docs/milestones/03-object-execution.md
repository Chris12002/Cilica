# Milestone 3: Objects, arrays, strings and dispatch

Status: in progress; slices 3a and 3b-a implement logical layouts and typed value-flow validation. Recorded 2026-10-01.

The next executable checkpoint is an independently authored small class created,
mutated and passed through calls, returning an Int32 result identical to .NET.
Managed references stay within the runtime. This milestone does not by itself
establish a collector, class-library compatibility or the stable embedding API.

## Proposed slices

| Slice | Outcome | Status |
| --- | --- | --- |
| 3a | Detached class identities, field storage and logical reference maps | Complete for bounded layout model |
| 3b-a | Concrete reference/null types and validated locals, joins, calls and returns | Complete for detached IR contract |
| 3b-b | CIL object translation, construction/field access and verified static/instance calls | Planned |
| 3c | Native object allocation, instance ownership and phase A root frames; first class differential execution | Planned |
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

## Next checkpoint and exit gates

Slice 3b-b must translate the validated reference contract from CIL, infer
compatible stack joins through backedges, and add explicit object operations.
Exact field declarations and access/readonly/initializer rules precede lowering.
Constructors need explicit receiver/signature and selected object-base
intrinsic binding; a method name alone is not an intrinsic. Object mutation must
preserve operand evaluation and null/fault order.

Slice 3c must prove lifetime before expanding the example: publish root frames
before allocation, preserve references across allocating calls, pop frames on all
exits, reject cross-instance ownership and destroy bounded experimental heaps.
Native offsets come from checked C++ layouts, not host metadata guesses. Null and
allocation failures need explicit semantics; foreign C++ exceptions do not supply
them. Differential results and native sanitizers are required before calling this
object execution support. Milestone 4 then supplies precise collection and stress.

The Alchemy/CoreLib general-filter gate remains unresolved. No object layout
or native checkpoint clears the library audit, catch/filter/finally, generics,
initialization, browser or mobile execution gates.
