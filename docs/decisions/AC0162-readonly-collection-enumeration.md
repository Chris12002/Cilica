# ADR AC0162: ReadOnlyCollection enumeration forwarding

Recorded 2026-10-08 for AC4.10.

## Evidence and decision

After AC8.49 the unchanged frozen engine passes PhysicalDamagePayload.Copy and
reaches ReadOnlyCollection<T>.GetEnumerator through SpellResourceAdmission.Evaluate
IL_0137. The diagnostic copy charges 1,114,599 metadata units in 3,680 ms;
StateLookup remains 15 methods, with 11,527 units. Evidence:
artifacts/alchemy-memberwise-clone-engine/work1572864.json. The reduced Lists probe
reproduces the missing managed-body CIL2001 before this change.

Use the pinned library/upstream/dotnet-runtime ReadOnlyCollection implementation's
observable control flow: generic GetEnumerator first reads list.Count, returns the
existing SZGenericArrayEnumerator<T>.Create(null, 0) shared empty cursor at zero,
and otherwise calls list.GetEnumerator. The nongeneric IEnumerable implementation
forwards directly to the backing list's nongeneric factory without reading Count.
Do not replace forwarding with an index loop: that would lose custom callbacks,
list version checks and distinct generic/nongeneric cursor behavior.

This is ordinary owned managed IL using already admitted list, array and custom
interface cursors. No new compiler operation, profile, grant, native layout or
resource limit is needed. Cursor element/storage support remains bounded by those
existing paths. ReadOnlyCollection copying and remaining nongeneric search/index
members remain explicit unsupported bodies.

## Validation and review

Nine probes cover typed/nongeneric list and array traversal, live array replacement,
list mutation faults and Reset, references retained after clearing, reference-bearing
value copies and boxing, shared empty identity and state faults, custom Count/factory/
MoveNext/Current/Dispose/Reset ordering, and exceptions from Count, factory, Current
and MoveNext. Foreach cleanup is compared with .NET on exceptional paths. A custom
empty nongeneric source throws from Count, proving that this path does not read it.
Generic empty enumeration bypasses the source factory even when the list later grows.

All seven focused checks pass in 37.790 seconds, with 2,646 repeated O0/O2 native/.NET
comparisons. ASan/UBSan, forced collection, exact static-root counts, 2,048-byte frame
checks and 30-second native subprocess caps remain active. Evidence:
artifacts/alchemy-readonly-enumeration/{red,tests}.log.

Review confirms the pinned generic/nongeneric difference, one Count evaluation,
existing empty cursor identity, live forwarding and existing fault/rooting paths.
Full engine, unchanged fingerprint execution and saved-game replay remain unverified.

Related regressions initially pass 37/38 checks. The remaining failure is an older
array test expecting the combined wide/narrow enumeration closure to be rejected.
It reproduces with the previously committed core (prior-core-boundary.log); no
new implementation change is needed. Promote the unchanged behavior to VariantClosure
and add 294 native/.NET comparisons. All 10 array enumeration checks then pass in
19.048 seconds (array-reviewed.log), preserving scalar-alias and exact-grant denial.
Together with read-only view, list and nongeneric array cursor regressions, this gives
45 distinct passing checks before the public baseline.

All four public baseline checks pass in 69.998 seconds: 46 native controls and one
expected diagnostic gap. Total validation is 49 distinct checks. All six frozen
input hashes still match the original audit; main 48ecf14 remains an ancestor.
