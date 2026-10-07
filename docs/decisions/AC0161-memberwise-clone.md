# ADR AC0161: Bounded sealed-class MemberwiseClone

Recorded 2026-10-08 for AC8.49.

## Evidence and decision

The unchanged frozen Alchemy engine reaches PhysicalDamagePayload.Copy IL_0001,
calling Object.MemberwiseClone from a private sealed application class. The prior
probe charges 1,108,818 metadata units in 3,702 ms; StateLookup remains 15 methods
and 11,517 units. Evidence: artifacts/alchemy-nested-static-fields-engine/work1572864.json.
The reduced clone fixture reproduces the unmapped declaration before this change.

Add the exact protected InternalCall declaration and ObjectMemberwiseClone grant.
Only the integer-object profile recognizes it. Discovery authenticates declaration
shape and grant, requires a sealed nonabstract caller class, and validates its entire
bounded application hierarchy ending at the selected Object. Translation requires a
receiver assignable to that caller. Logical and scalar IR independently recheck those
facts. ObjectClone carries the validated exact type; the constructor validator rejects
cloning an uninitialized this before base construction.

Use the existing typed allocator followed by individual physical field assignments,
including flattened base fields and value leaves. References stay shallow and values
are copied. Do not invoke constructors or copy the object header. This preserves new
identity, ownership, heap links and collector state. Allocation is a safepoint, with
receiver roots retained; no further allocation occurs during copying. Generated calls
check null, ownership and exact sealed type before accessing native storage. Failure
uses the existing managed allocation-fault path. Boxes and special owned-core objects
cannot enter this operation through its application-class receiver contract.

Open classes and inherited runtime objects remain unsupported rather than guessing
a dynamic object layout. CIL v72, object IR v46 and native v72 identify the new operation;
layout v26, interface plan v16 and all production/diagnostic budgets are unchanged.

## Validation and review

Eight probes exercise inherited/private fields, wide values, readonly references,
embedded reference-bearing values, arrays, self-references, constructor suppression,
exact type and distinct identity, repeated clones, null receivers, empty classes and
closed generic classes. They provide 2,352 repeated O0/O2 native/.NET comparisons with
ASan/UBSan, forced collection, exact cleanup, 2,048-byte frame checks and 30-second
native subprocess caps. Additional native checks cover object/byte quotas, fresh header
fields, managed allocation faults, frame/borrow cleanup and recovery after failure.
Grant denial, nonsealed and legacy profiles, forged type/operand/result packets in both
IR stages, and cloning this before base initialization are rejected.

Initial native probes exposed metadata base shapes with Unknown class kind; discovery
now admits that normal metadata representation while the complete resolved hierarchy
and both IR validators still enforce class layouts. A quota assertion initially expected
a managed fault at the allocator layer. The corrected check verifies allocator null
with no pending fault, plus a separate generated managed call producing the fault and
recovering. No allocator semantics or guard was changed for the test.

All 42 selected clone, constructor, identity, collection and boxed-value checks pass
in 19.990 seconds. Evidence: artifacts/alchemy-memberwise-clone/reviewed-tests.log.
Review checked grant shape and access, full base-chain validation, sealed exact types,
constructor initialization, allocation rooting, shallow field copies without header
copying, empty/generic native layouts and unchanged box escape restrictions.
Full engine, unchanged fingerprint execution and saved-game replay remain unverified.

All four public baseline checks pass in 68.786 seconds: 46 native controls and one
expected diagnostic gap on native v72. Total validation is 46 distinct checks.
Main 48ecf14fd7a2c72e63281b50a6a61ea1c090c187 remains an ancestor.
