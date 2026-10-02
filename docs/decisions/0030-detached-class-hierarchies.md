# ADR 0030: Detached class hierarchies and ancestor assignability

Status: accepted for the bounded layout and detached reference-IR APIs.
Recorded 2026-10-02.

## Context

Milestone 3d needs inheritance, casts and verified dispatch. Previously every
layout had to derive directly from the selected Object base, and reference
assignments could widen only to that base. Changing the native emitter directly
would mix declaration identity, field representation, constructor ordering and
dispatch in one change. Establish the checked hierarchy first; the runtime's
existing resource and root contracts remain intact.

## Decision

1. `object-layout-v2` includes the complete base-class and reference-field
   closure of its explicit roots. Classes remain concrete, nongeneric and
   automatic-layout. Every chain must end at the selected fieldless, unsealed,
   parentless Object definition. Reject missing/non-class bases, cycles and
   derivation from a sealed class. Sealed leaves remain valid. The existing
   exclusions for abstract classes, interfaces, generic owners, initialization,
   explicit layout and unsupported field storage remain.

2. Keep `ObjectTypeLayout.Fields` and `ReferenceSlots` declaration-local. A
   base field retains the base class's image/token identity; a derived field
   hiding the same name is a different declaration. Do not flatten fields into
   every descendant or invent target byte offsets. Later native lowering must
   compose storage and tracing maps explicitly.

3. Count ancestors against the existing 256-type, 4,096-field and 65,536 metadata
   work limits. Fields are counted once per declaration. Validate base chains
   iteratively in at most 256 squared steps; cycles cannot consume the host call
   stack. The selected Object type counts toward the limit. The separate
   64-enclosing-owner limit is unchanged and does not cap inheritance depth.
   Inclusion diagnostics distinguish base edges from reference-field edges;
   invalid metadata returns no partial model.

4. `typed-reference-ir-v2` allows derived references wherever an ancestor type
   is required, including locals, call arguments, returns and declared block
   inputs. Preserve exact load/call result types; accepting an assignment does
   not permit relabeling the source value. Reject implicit downcasts and sibling
   conversions. Names and tokens alone never imply identity across images.
   Null remains assignable to reference storage under the existing rules.
   Cache at most 256 squared ancestry identities and share that cache across
   methods during program validation. Reachability includes ancestor fields.

5. Keep the executable object profile limited to direct-Object classes. Its
   validator explicitly rejects inherited catalogs before native emission or
   root assignment; CIL translation reports that boundary as an actionable
   error. The current emitter must not reinterpret derived storage as a base
   struct before a legal C++ object layout exists. This slice provides no native
   inheritance, constructor chaining, casts, abstract classes or virtual/interface
   dispatch. CIL join inference and native ABI/profile remain unchanged.

## Validation and follow-up

Reduced metadata fixtures cover hidden fields, ancestor reference cycles,
cross-image same-name bases, sealed leaves, inheritance cycles, sealed/missing
bases, unsupported ancestors with inclusion paths, and exact/excess type/field
budgets. A 256-type chain exercises nonrecursive model and reference validation.
All 25 source/target pairs in an independently authored .NET hierarchy serve as
an assignability oracle. Detached IR checks local/call/return/join widening,
exact value tags, unrelated catalog roots, and both CIL/native rejection gates.

Review covered malformed graph termination, sealed/selected-base rules,
declaration identity, field closure, ancestry-cache lifetime, and the native
capability boundary. It caught eager diagnostic-name construction on valid
ancestor steps; failure-only formatting and a deep-graph allocation regression
prevent long names from amplifying validation memory. No further issue was
identified within the detached contract.

Validation: build succeeds with zero warnings/errors. All 504 targeted native,
object, reference-IR, C++ and Int32 cases pass in 38.0 seconds. After the review
fix, all 145 focused hierarchy/model/reference cases pass in 0.43 seconds,
including the new allocation guard. Existing O0/O2 sanitizer, arena-budget and
native-stack regressions remain intact. Local main and cached origin/main are
ancestors; no push, hosted work or additional agent was used.

The next slice must design inherited native field storage and constructor-chain
validation together, including base-first initialization, ownership checks and
root publication across allocating base constructors. Casts and dispatch follow
that executable layout. Helper batching remains deferred under ADR 0029 and is
not an entry gate for these capability slices.
