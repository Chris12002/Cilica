# ADR AC0232: Stable local and parameter addresses through branches

Recorded 2026-10-09 for AC8.88 after a0c6dd4 (AC8.87).

Frozen InitiativeSlots.For (0x06000013 IL_0015) keeps a local closure address on
the stack while branching around a cached delegate. Converted Alchemy.Engine.cpp
lines 5850-5885 assigns the same &V_0 address on both paths. SHA256:
`f4762054c04bf5a9f7a2dc45b837171b4b7bd2ea82073975d56bd07b1215b10f`. The converted capture guides the storage model; frozen IL remains
authoritative and all input DLLs are unchanged.

## Decision and review

Generalize the existing direct-receiver rematerialization proof to exact
AddressLocal, AddressParameter and borrowed Parameter expressions in the
integer-object profile. Every predecessor must name the identical source slot.
Recreate that expression in the successor and omit it from physical block inputs
and edge arguments. Slot contents are not snapshotted or reloaded. Existing method
frames/rooted locals and checked borrowed parameters retain storage for the whole
call; no new native location transport, lifetime or collection rule is introduced.

Addressed value parameters may change contents but keep their slot; borrowed
parameters cannot be rebound or have their own address taken. Existing constructor
receiver stability checks remain. Exact type, width and readonly validation still
applies at the recreated expression. Different local/parameter origins, heap-field
and array-element addresses remain diagnostic. A branch selecting distinct slots
needs a separate design; it must not silently choose one slot.

Review checked source identity on all incoming edges, mixed rematerialized/value
stack indices, loop revisits, constructor stability, exception entry stacks and
unchanged native borrow ownership. Both logical/scalar validation still reject
managed-location block inputs. CIL112 changes; IR65/native81, physical layouts,
core/grants and production resource limits are unchanged. No remaining findings
for identical stable slots.

## Validation

Artifacts/alchemy-location-joins: 59 distinct selected checks pass across the
initial and final runs. The initial 54-check run passed 52 in 54.259s; two attempted
promotions of generic receiver replacement failed because the branches select
different slots. Those cases retain an explicit, more precise diagnostic. The
final 25-check run passes in 39.853s, covering the corrected negative, new boundary
cases and the adjacent field-return proof. Earlier tuple/value successes remain
valid: the compiler was unchanged between these runs.

Six new C# slot roots, one raw same-slot join and two promoted conditional tuple
construction roots x 49 pairs x three repeats x O0/O2 = 2,646 new native/.NET
comparisons. Tests cover local slots, addressed value arguments, incoming borrowed
arguments, reference-bearing values under allocation, changed slot contents, fault
order, exact-slot joins and older-profile rejection. Distinct origins and heap/array
addresses remain rejected. ASan/UBSan, forced collection, exact root/borrow cleanup
and frame-budget checks remain enabled. No full suite, hosted or device tests ran.

## Combined frozen inventory

The fresh diagnostic-only scan includes AC8.87's array comparison and this slice.
It discovers 15,253 bodies and translates 15,225 individually: 28 failures remain,
down from 43. Fourteen of the 21 location-join failures clear, and the array-length
failure clears. Six remaining joins select distinct slots; one carries a derived
address. Other failures: 11 exact-storage mismatches, eight ldsflda operations and
two integer-parser grammar proofs.

Engine work is 3,818,341 metadata units in 67,387 ms. Individually successful
bodies total 60,922 blocks, 288,452 statements, 1,000 exception regions, 9,496 inputs,
13,723 edge arguments, 27,269 parameters, 14,989 locals and 76,947 call arguments.
StateLookup translates 15 methods in 348 ms. The inventory always returns no
program and emits no C++; production aggregate limits are not lifted. Full IR/native
closure and saved-game replay remain incomplete. Receipts and exact copied-source
hashes: artifacts/alchemy-location-join-inventory. Core/manifest pins are unchanged
from AC8.85; frozen input receipts retain the six verified hashes.
