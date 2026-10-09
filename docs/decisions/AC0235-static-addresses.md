# ADR AC0235: Checked borrows of per-instance static storage

Recorded 2026-10-09 for AC8.91 after 32ef9cf (AC8.90).

Frozen EffectiveSpellCompiler.Failure (0x060009d4 IL_0000) borrows the static
readonly Rejections.SpellEffectUnsupported value and calls its getter. Converted
Alchemy.Engine__6.cpp lines 15598-15611 performs type initialization before passing
the static field address. SHA256:
`21455d5f4a89a1a37a981cbe305a4d91c5dfab7c553f63b5bcd738eb91368886`.
The converted capture differs from the frozen build; it guides behavior only.
No application input or Unity runtime implementation is copied or changed.

## Decision

Discover ldsflda through the existing static-field binder, access rules and
BeforeFieldInit initializer plan. AddressStatic is an explicit throwing IR
operation, charged as an allocation safepoint because static storage may need
allocation. Both IR stages require declared storage, access, exact borrow type,
readonly capability and the fault edge. Static usage/visibility accounting includes
address-only fields. Constant-default readonly reads keep their optimization;
address requests materialize the same zero-initialized runtime slot.

Logical inline addresses expand into the existing scalar static components.
Empty values retain a dummy slot operation so initialization/allocation checks
are not erased. Mutable borrows alias the original storage; no copy or writeback
can alter exception ordering or hide aliasing. Reference leaves remain covered by
the existing static-root tracer.

The native static_slot carrier carries a globally issued active frame token and
slot index. Borrow registration now includes methods whose only addresses are
static. Validation authenticates that token in the current context before using
that instance's slot, then checks immutable kind/readonly/reference-predicate
metadata. No raw host address is exposed. Expired tokens, foreign contexts,
wrong kinds and wrong/foreign reference objects are rejected without a store.
Actual typed StaticSlot members preserve C++ object provenance. Clearing readonly
on a carrier cannot bypass readonly field metadata.

Readonly static addresses remain readonly even in a type initializer; ordinary
stsfld initialization remains supported. Narrow scalar addresses, String.Empty's
specialized address, strict type/module initializers, escaping borrows and branch
joins of derived/static addresses remain diagnostic. This is not a general native
pointer feature. CIL115/object IR68/native84 identify the new operation and carrier;
no grants, core library code, physical object layout or production budgets change.

## Review and validation

99 distinct selected tests pass. StaticAddressTests adds 11 runtime probes plus
logical/scalar declaration/type/readonly/fault tampering, metadata access checks
and O0/O2 native carrier tests. An existing StaticValue address rejection is
promoted to two native comparisons; the default-static address case now asserts
a readonly borrow and successful lowering. The 98-test adjacent run had 96 passes
in 61.555s and those two obsolete negative expectations; seven targeted final
checks pass in 2.553s after promotion. Initial 28 static-address checks passed in
23.598s. Adjacent suites: AssemblyStatic, DefaultStatic, StaticValue and
NativeLocationCarrier. No full-suite, hosted or device runs.

The 12 new/promoted roots add 3,528 native/.NET comparisons (49 pairs x three
repeats x O0/O2). ASan/UBSan, forced collection, exact roots/borrows and frame budget
checks remain enabled. Coverage includes aliasing, wide/reference/nested/empty
values, readonly/default/closed-generic statics, initializer fault order and
persistent storage. Native carrier checks cover retired/replaced registrations,
foreign instance and sibling contexts, bad slot/owner/descriptor/element/width,
readonly metadata, wrong reference types and GC retention/release.

The first run caught a missing frame registration for methods using only static
addresses; ObjectFrameLayout now registers them. The fixture also initially shared
a readonly object initializer across unrelated probes, retaining an extra expected
root. It now isolates that initializer. A default Guid probe compiled to a copy
and hit the existing internal default-read access restriction; a readonly generic
struct exercises the intended address path instead. No application code changed.
Review checked initialization placement, fault edges, dummy empty slots, tracing,
frame lifetime, C++ typed access and detached validation. No remaining findings
within the admitted slice. Receipts: artifacts/alchemy-static-addresses.

## Frozen inventory

All eight static-address failures clear. The diagnostic-only scan individually
translates 15,246 of 15,254 bodies; eight fail: six different-slot joins, one
derived-address join and ldvirtftn in ForcedMovementOperation.Run. Discovery uses
3,839,223 metadata units in 63,525 ms. Successful bodies total 61,515 blocks,
292,791 statements and 1,012 exception regions. StateLookup translates 15 methods
in 327 ms, using 11,759 units. The copy returns no program or native emission;
aggregate production capacity, full IR/native closure and replay remain incomplete.
All six frozen input hashes remain pinned.
Core SHA256: `9c18b9882eab4b53a1fc1b174dcfae1d29ccafa5a86b5b7f159e0aea7e6e4b86`.
Manifest SHA256: `de2afb930dace804894d63e281862349a177ef950bc495fa4960e1f2a9a6c301`.
Receipts: artifacts/alchemy-static-address-inventory.
