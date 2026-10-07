# ADR AC0157: Three-component tuple storage and typed equality

Recorded 2026-10-08 for AC5b.15.

## Evidence and decision

Committed Guid.Parse support (bb701d3) advances unchanged frozen engine discovery
to ReduceShieldPointsPrimitive.Validate IL_0001. Its Read method returns
ValueTuple<EffectSourceSelector,Int32,Int32>; the selected netstandard declaration
0x02000100 has no mapping. Discovery charges 824,013 metadata units in 3,375 ms;
StateLookup remains 15 methods, now 11,517 units due to the owned metadata change.
Evidence: artifacts/alchemy-guid-static-parse-{refresh,engine}. No engine execution.

Add the three-field managed tuple using the existing five-field pattern and the
pinned ValueTuple.cs field and typed-equality contract. Each component is copied
before KeyOperations.Equal, preserving default comparer value semantics; each
later field is read only after the preceding comparison. Unsupported members retain
the existing explicit UnsupportedLibraryMember marker. No native operation,
intrinsic grant, compiler profile or resource limit changes. Add the exact nominal
mapping to the test and diagnostic harnesses; application assemblies remain frozen.

## Validation and review

The reduced Fields probe first reproduces the exact CIL3015 contract-token gap.
Ten probes then add 2,940 repeated O0/O2 native/.NET comparisons for fields, independent
copies, wide/reference returns, heap/vector storage and reference aliasing, boxes,
nullable values, string/null equality and ordered mutable-struct equality callbacks.
Five unsupported paths remain diagnostic, including the existing rule against a
managed location crossing a conditional construction join. All 44 tuple-family
checks pass in 29.318 seconds. Evidence: artifacts/alchemy-tuple-three/{red,tests}.log.
Native checks retain ASan/UBSan, forced collection, exact cleanup, 2,048-byte frame
checks and 30-second process caps. All six original frozen assembly hashes match.

Review checked nominal arity and fields, typed-equality copy and short-circuit order,
reference rooting, unchanged unsupported marker bodies and mapping scope. Provenance
also corrects the earlier Guid summary to reflect the already implemented static
Parse entry point. Full engine, unchanged fingerprint execution and saved-game replay
remain unverified.

All four compatibility baseline checks pass in 68.433 seconds: 46 native controls
and one expected diagnostic gap. Total validation is 48 distinct checks. Main
48ecf14fd7a2c72e63281b50a6a61ea1c090c187 remains an ancestor.
