# Milestone 3: Objects, arrays, strings and dispatch

Status: in progress; slice 3a implements logical class/field models only. Recorded 2026-10-01.

The next executable checkpoint is an independently authored small class created,
mutated and passed through calls, returning an Int32 result identical to .NET.
Managed references stay within the runtime. This milestone does not by itself
establish a collector, class-library compatibility or the stable embedding API.

## Proposed slices

| Slice | Outcome | Status |
| --- | --- | --- |
| 3a | Detached class identities, field storage and logical reference maps | Complete for bounded layout model |
| 3b | Typed reference IR, construction/field access and bounded static/instance calls | Planned |
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

## Next checkpoint and exit gates

Slice 3b must integrate concrete reference types and null into typed values,
locals, block inputs and calls. Joins cannot accept arbitrary reference/integer
mixes. Exact field declarations and access/readonly/initializer rules precede
lowering. Constructors need explicit receiver/signature and selected object-base
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
