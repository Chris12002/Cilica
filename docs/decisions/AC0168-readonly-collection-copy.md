# ADR AC0168: Generic read-only collection copies

Recorded 2026-10-08 for AC3.28.

The frozen GameState constructor reaches ReadOnlyCollection<T>.CopyTo through
List<T>.CopyEnumerable. Implement the pinned generic method by forwarding directly
to the backing IList<T>.CopyTo. Do not add Count reads, argument validation, cursor
allocation or a replacement copy loop: custom backing lists own those behaviors.
The existing mapped method and managed interface call path suffice. Nongeneric
ICollection.CopyTo remains unsupported. Profiles, grants and all bounds stay unchanged.

Eight probes provide 2,352 repeated O0/O2 native/.NET comparisons with forced GC,
ASan/UBSan, frame-budget and cleanup checks. They cover live List/array sources,
self copies, inherited reference identity, reference-bearing value copies, exact
argument exceptions, covariance faults with partial stores, custom callbacks and
the frozen engine's List(read-only-view) snapshot pattern. The custom IList throws
from Count/index/enumerator access, proving the public copy uses only its CopyTo.
It also accepts otherwise-invalid arguments and can throw after writing output.

All positive probes and adjacent List copy/read-only cursor tests pass. The initial
negative assertion expected the owned unsupported-body diagnostic, but conservative
array-interface discovery rejects nongeneric copying earlier. Pin that existing
runtime-helper diagnostic; no compiler behavior was changed to satisfy the test.
All 30 distinct selected checks now pass, including the 46-control public native
baseline and its one retained gap.
Review confirms the single forwarding operation preserves backing validation and
mutation, and introduces no runtime ownership or native arithmetic changes.
Evidence: artifacts/alchemy-readonly-copy{,-refresh,-engine}.

The unchanged engine advances to Dictionary<TKey,TValue>.KeyCollection.CopyTo,
again through List<T>.CopyEnumerable, this time from SettleHandLimits. Diagnostic
work is 1,147,132 charged metadata units in 4,048 ms. StateLookup remains 15 methods
and 11,527 units (438 ms). Diagnostic capacities and 60-second command limits are
unchanged. Full engine/fingerprint execution and saved-game replay remain unverified.
