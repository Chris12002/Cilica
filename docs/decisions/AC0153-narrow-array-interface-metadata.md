# ADR AC0153: Byte/character array interface metadata

Recorded 2026-10-07 for AC7a.7.

## Evidence and decision

After e37aafd, the frozen engine clears delegate array helper metadata and stops
at the blanket narrow-vector interface guard for nongeneric collection Count
through Enumerable.Count. Discovery charges 782,813 metadata units in 2,690 ms;
StateLookup remains 15 methods/11,511 units. No engine native emission/execution.

Count, collection flags and SyncRoot depend only on array length/identity, not
on element width or boxing. Admit only the existing managed Count, False, True
and Identity helpers for Byte/Char vectors. Preserve exact Byte/Char generic
helper parameters and results rather than converting their array views to Int32.
Generic narrow contracts must name the exact Byte or Char argument; signed aliases
remain diagnostic even though their type tests are separately modeled.

The detached recipe and target validators enforce the same operation and exact
contract boundary. Element loads/stores, CopyTo, enumeration, Clear and other
operations remain outside this slice. No intrinsic grant or native helper is
added. Existing managed helper bodies still undergo ordinary translation and
validation. Integer CIL v69, object IR v44, interface plan v16 and native v70
record the admitted packet shapes; layout v26, core and budgets are unchanged.

## Validation and review

The reduced Count probe reproduces the blanket-guard failure before the change.
Five positive probes cover nongeneric/generic Count, fixed/read-only/synchronization
flags, Byte/Char SyncRoot identity across forced collection and null receivers.
They provide 1,470 repeated native/.NET comparisons at O0/O2 with ASan/UBSan and
forced collection. Five negative operations retain diagnostics: indexed Char
read, Byte CopyTo, Char enumeration, nongeneric Clear and a signed generic view.
Existing narrow borrows and signed-array construction remain rejected.

Detached checks reject element-access helper substitution and a Byte-to-Char
helper packet both before and after scalar lowering. Review identified that the
signed-view boundary also needed explicit target validation; a forged signed
Count contract is now rejected at both stages. The positive generic Count probe
includes the separately valid signed type test, so the signed catalog facts are
real rather than invented for this mutation.

All 113 distinct focused narrow-vector, enum/delegate array, generic interface
and detached-plan checks pass. Evidence: artifacts/alchemy-narrow-array-metadata/
{red,tests,review-tests}.log. Review checked exact widths, metadata-only operations,
null checks, SyncRoot identity and retention, catalog assignment, helper grants,
forged contracts and unchanged native ownership. No frozen input or game rule
changed. Full engine, unchanged fingerprint execution and saved-game replay remain
unverified.

All four public-baseline checks also pass: 117 distinct checks overall, 46 native
controls and one retained diagnostic gap. Evidence: baseline.log. Local main
remains ancestor 48ecf14.
