# ADR AC0234: Readonly value and primitive receivers

Recorded 2026-10-09 for AC8.90 after f0e19e4 (AC8.89).

Frozen ChangeCreatureControlOperation.Apply (0x06000abb IL_0006) borrows the
readonly _target field to call EffectTarget.get_Unit. The type, rather than that
getter, carries IsReadOnlyAttribute. Converted Alchemy.Engine__6.cpp 23372-23378
likewise passes the field address; its getter at 12472-12478 only loads the value.
SHA256: `21455d5f4a89a1a37a981cbe305a4d91c5dfab7c553f63b5bcd738eb91368886`.
The converted capture differs from the frozen input build and is reference
material only. No application code or Unity runtime implementation is adopted.

## Decision

Read the reserved IsReadOnlyAttribute name from the declaring type or method into
both body and declaration snapshots, charging each inspected attribute against the
existing metadata ledger. Embedded definitions are recognized: this marker only
restricts access and never authorizes a mutating body. Ordinary value/primitive
instance bindings retain that fact and require the exact readonly receiver type.
Constructors remain writable; static/reference methods cannot carry this binding
flag. Closed snapshots preserve it through existing record copies.

Scalar lowering expands a readonly value receiver into readonly scalar/reference
borrows and clears the now-inapplicable instance flag when making static helper
bindings. Boxed interface plans retain their original declaration and validate
both logical and lowered receiver packets. Value-key and boxed-renderer checks
likewise honor the declared capability. No native borrow representation changes.
A readonly struct can still mutate an object referenced by a field; only its own
storage is readonly. C# defensive copies for mutable methods remain intact.

Owned Char/Int32/UInt32/Int64/UInt64 readers and Nullable HasValue/Value/both
GetValueOrDefault overloads are marked readonly. Nullable.ToString deliberately
remains mutable, preserving mutations by a payload override. Existing readonly
Guid and KeyValuePair declarations now acquire the same receiver contract.

CIL114/object IR67/native83 identify the changed contract. Intrinsic grants,
physical layouts, native algorithms and production resource limits are unchanged.

## Review and validation

119 distinct selected tests pass. The initial 35-test run covers ReadOnlyReceiver,
BoxedInterface and ValueLocationLowering tests (34.812s). Of 84 adjacent checks,
83 passed in 146.369s; one PrimitiveInstance assertion expected the old writable
signature. Updating it to assert readonly metadata and storage passes on the
final targeted run, alongside body/declaration exact and one-short read budgets.
Adjacent suites include GuidLibrary, NullableLibrary, NullableText,
NullableFormatting and both declaration readers. No full suite or device run.

Nine new roots add 2,646 native/.NET comparisons (49 pairs x three repeats x O0/O2),
with ASan/UBSan, forced collection, exact root/borrow cleanup and frame-budget
checks. They cover type/member markers, closed generics, primitive/nullable/Guid
readers, reference-bearing values, boxed dispatch, defensive copies and null fault
order. Forged writes fail both logical and scalar validation; metadata-only
assemblies with falsely annotated mutating bodies fail translation.

The first fixture initialized a nested member through a readonly field, hitting an
existing separate constructor-field-address restriction. The fixture now assigns
a fully constructed value to isolate receiver semantics. That broader constructor
case remains unsupported; no application input was changed. Review checked
constructor capability, scalar propagation, legal referenced-object mutation,
nullable mutable text behavior, attribute accounting and both validator gates.
No remaining findings in the admitted slice. Receipts: artifacts/alchemy-readonly-receivers.

## Frozen inventory

The rebuilt-core diagnostic inventory clears all 11 readonly mismatches. Of 15,254
discovered bodies, 15,238 translate individually and 16 fail: eight ldsflda sites,
six different-slot joins, one derived-address join and a newly exposed ldvirtftn
in ForcedMovementOperation.Run (0x06000cb1 IL_0355). It records the first failure
per body, so later failures can still emerge.

Engine discovery charges 3,839,151 metadata units in 63,888 ms. Successful bodies
total 61,110 blocks, 289,847 statements and 1,002 exception regions. StateLookup
translates 15 methods in 333 ms and charges 11,759 units. Aggregate production
limits remain exceeded; the diagnostic copy returns no program and emits no
native code. Full IR validation, engine compilation and saved-game replay remain
incomplete. The six frozen input hashes remain pinned.
Core SHA256: `9c18b9882eab4b53a1fc1b174dcfae1d29ccafa5a86b5b7f159e0aea7e6e4b86`.
Manifest SHA256: `0f5eb0100f1c0f41885a16420405b4d92b073306032f96e1ddec373efaf56957`.
Receipts: artifacts/alchemy-readonly-inventory.
