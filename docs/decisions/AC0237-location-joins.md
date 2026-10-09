# ADR AC0237: Checked managed-location branch inputs

Recorded 2026-10-09 for AC8.93 after bf522e5 (AC8.92).

Six frozen EffectOperationInstruction<T>.Run/ResumeChild bodies select between a
parameter and a temporary reference slot. ActivationCountDamageOperation.Run
carries a derived address over a branch. AC8.88 could only rematerialize the same
local/parameter address on every edge, so these required actual location inputs.

Converted Generics__7.cpp 16822-16935 corroborates the first pattern: G_B2_0
selects the address of scope or V_2 before the interface call. SHA256:
`3ea7adc635909ac7de6b2b16eb7da0126f98a25e542fb40d05824a8b7ccc7ee4`.
This capture uses different assemblies from the frozen audit. Its pointer
algorithm guides the semantic test; no Unity code or runtime is substituted.

## Decision and lifetime proof

The integer-object profile carries typed ManagedLocation block inputs through
existing logical/scalar packets. Value borrows expand to their component locations,
including reference components. Edge type joins preserve exact element and narrow
storage identity; mutable locations may widen to readonly, never the reverse.
The emitter copies each carrier into a distinct input slot, publishes its owner
root immediately and applies the target's readonly bit. Source value and input
slots are disjoint, including self edges, so simultaneous moves cannot overwrite
a source. This requires no new native location representation or raw pointer.

A frame/parameter/static borrow retains its existing active-frame token. No edge
outlives its invocation; callees are synchronous and cannot return managed
locations or store them in heap fields. Heap/array borrows carry separately rooted
owners. Entry methods cannot receive an ownerless external borrow, and exception
handlers still require their exact clause-entry stack. Existing local, return,
call, store, address, owner/type and frame authentication checks remain in force.
Root/frame/scalar expansion budgets count the new input slots. Legacy profiles
retain their prior join boundary.

Reference-constructor this and value-constructor this are still rematerialized
with exact Parameter(0) identity. That proof authorizes readonly initialization;
branches cannot silently substitute another location for it. The first full-body
inventory exposed this distinction in DecimalParts::.ctor when the initial
implementation transported value-constructor this as an ordinary input. Restoring
its identity fixed the regression; a conditional readonly-field constructor is
now an executable reduced test. Other value/primitive instance borrows use the
new input path.

CIL117/object IR70/native86 identify this contract. Core library, intrinsic grants,
physical layouts, native lifetime algorithms and production budgets are unchanged.

## Review and validation

Review covered type variance, readonly widening, constructor identity, exception
entry stacks, packet expansion, owner publication, cyclic edge copies, token
lifetime and escape prohibitions. No remaining findings within this scope.

Focused native tests include stable and distinct locals/parameters, heap fields,
arrays, mixed origins, nested reference-bearing values, readonly receivers, wide
integers, reference replacement, statics, empty values, selected-operand fault
order, constructor initialization and a two-location self-loop swap. The generic
receiver replacement tests now execute: a null receiver remains null even when
argument evaluation changes the referenced variable, matching .NET. Both IR
stages reject forged readonly, element and nested-location join types. Every
scalar location input has a distinct owner root. Native checks use O0/O2,
ASan/UBSan, forced collection, cleanup and exact/one-short frame budgets.

Initial fixture corrections: catch the intended .NET NullReferenceException in
the generic replacement oracle; sanitize generated C++ probe names; exercise a
readonly receiver rather than unsupported general in-parameter metadata. These
changes do not alter frozen inputs or conceal a native mismatch.
Receipts: artifacts/alchemy-general-location-joins.

Final results: 181 distinct selected tests pass: 29 initial focused tests (39.126s),
two additional loop checks (3.207s), 124 adjacent checks (44.325s), and the final
42-test LocationJoin/Decimal/ValueConstructor run (84.373s), accounting for overlap.
21 new/promoted roots provide 6,174 native/.NET comparisons (49 pairs x three
repeats x O0/O2). No full suite, device or engine-native run is claimed.

The first inventory cleared all seven previous failures and exposed the
constructor-this regression (15,253/15,254). After its reduced fix, the second
inventory completes all 15,254 bodies: 61,756 blocks, 294,454 statements, 10,210
inputs, 14,705 edge arguments, 27,376 parameters, 15,366 locals, 79,949 call arguments
and 1,021 exception regions. Discovery charges 3,839,228 metadata units in 63,374ms;
StateLookup completes 15 bodies in 341ms/11,759 units. Each inventory reports only
the first failure per body and returns a null program. Full-program validation,
production capacity, native emission and saved-game replay remain unfinished.
Receipts: artifacts/alchemy-general-location-join-inventory and
artifacts/alchemy-general-location-join-inventory-r2.
Frozen inputs remain pinned. Core SHA256:
`9c18b9882eab4b53a1fc1b174dcfae1d29ccafa5a86b5b7f159e0aea7e6e4b86`.
Manifest SHA256:
`8e95e8f55891137b93a7fbbc0b6795b4f1f1cc348b03543c7d990f50d3e05c8f`.
