# ADR AC0158: Two-argument HashCode.Combine

Recorded 2026-10-08 for AC5a.6.

## Evidence and decision

Committed tuple support (71b1509) advances frozen engine discovery to
EffectProgramIdentity.GetHashCode IL_000c after 963,343 metadata units (3,581 ms).
The missing netstandard declaration 0x02000075 is HashCode; Alchemy reaches
Combine<String,String> and separately uses Combine<Int32,Int32> for WaveGroupId.
StateLookup remains 15 methods/11,517 units. Evidence:
artifacts/alchemy-tuple-three-{refresh,engine}. No engine emission or execution.

Add only the exact static generic two-argument member on the owned HashCode struct.
It hashes each copied parameter in order with the existing granted closed key
operations, substitutes zero for null and calls PairHash. Reuse the existing pinned
xxHash32 adaptation and fixed zero seed. Numeric hashes are implementation-specific,
not portable identities. There is no new grant, storage representation, native
operation or compiler profile. Other arities, mutable builder APIs and currently
unadmitted key categories remain diagnostic. The exact nominal mapping and owned
source are added to the local build/probe harnesses.

## Validation and review

The reduced static call first reproduces the same CIL3015 token gap. Six probes add
1,764 repeated O0/O2 native/.NET behavioral comparisons for stability, null/exact
UTF-16 keys, wide values, mutable value hash callbacks/copy isolation/fault order,
reference hash callbacks and dictionary growth/replacement/removal. A seventh probe
adds 294 native comparisons to an independent pinned-algorithm model with the
Cilica seed; it does not compare randomized .NET numeric hashes. Negative checks
cover other arity, nullable/array components and mutable builder APIs.

The initial 29-check run passes 28 and exposes an older array-key diagnostic mismatch.
A separate harness using the unchanged committed pre-HashCode core reproduces the
same invalid-layout diagnostic (prior-core-boundary/result.json). Array key equality
and hashing already require unsupported special receiver dispatch. Reject them
earlier with an actionable message, preserving IsNull and all detached validators.
Eight focused checks pass after this correction in 15.078 seconds, yielding 29
unique passing checks overall. The tuple nominal metadata test now also covers
arity three. Evidence: artifacts/alchemy-hashcode-pair/{red,tests,reviewed-tests}.log.
Native checks retain ASan/UBSan, forced collection, exact cleanup and existing caps.

Review checked parameter-copy ownership, callback/null/fault order, wrapping hash
arithmetic, existing grant enforcement, the fixed-seed boundary and array rejection
before catalog construction. Full engine, unchanged fingerprint execution and saved
replay remain unverified. Production and diagnostic resource limits are unchanged.

All four public baseline checks pass in 68.818 seconds: 46 native controls and one
expected diagnostic gap. Total validation is 33 distinct checks. Main
48ecf14fd7a2c72e63281b50a6a61ea1c090c187 remains an ancestor.
