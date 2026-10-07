# ADR AC0163: Bounded LINQ Cast and iterator lifecycles

Recorded 2026-10-08 for AC6.17.

## Evidence and decision

After read-only enumeration, the unchanged frozen engine reaches Enumerable.Cast
from PhysicalDamagePayload.Resolve IL_124b. Its source is the published
IReadOnlyList<PhysicalDamage>, downcast to EnvironmentalEntryDamage before ToArray.
The diagnostic copy charges 1,115,913 metadata units in 3,604 ms; StateLookup remains
15 methods/11,527 units. Evidence: artifacts/alchemy-readonly-enumeration-engine/work1572864.json.
The reduced TypedShortcut fixture reproduces CIL3010 before this change.

Archive Cast.cs and Cast.SpeedOpt.cs from the already pinned dotnet/runtime commit
79d0c463f1b55624c874a11585f7e47731e8d675 and record their hashes in library/provenance.json.
Retain the public branch order: return an IEnumerable<TResult> source unchanged,
then reject null, then select the ICollection specialization or general yield path.
Both new helpers use ordinary owned managed IL and existing casts/interface dispatch.

CastIterator models the generated foreach/yield state machine. First same-thread
GetEnumerator can reuse the query; subsequent cursors are independent. MoveNext
acquires the source lazily and catches managed Exception to dispose before rethrow.
Dispose terminates an unstarted cursor too; Current remains retained. Normal end
clears the source cursor after disposal. All admitted faults are managed exceptions;
foreign non-Exception throws remain outside the profile.

CastCollectionIterator preserves the pinned Iterator behavior: Current clears on
successful disposal, and MoveNext faults do not silently dispose. If source disposal
throws, the original cursor/current state remains, so a caller can observe repeated
cleanup. Count is the source count; Any uses a nonnegative cheap count or TryFirst.
ToArray allocates the reported length then casts/stores each source item; ToList
uses it as initial capacity. This preserves wrong-count faults and callback order.
First and Contains traverse the source independently and dispose in finally.

Existing query interfaces preserve Select's cheap counts, First disposal before
selector calls, full Count traversal and presized materialization. Empty or invalid
presizing must not consume the original query's first cursor. No new compiler profile,
intrinsic grant, native layout, production/diagnostic capacity or timeout is added.
Public signatures remain those of the mapped netstandard2.1 declaration; these owned
helpers are not a promise of .NET's private iterator names/layouts.

## Validation and review

Seventeen probes cover typed source identity/covariance, nulls, reference downcasts,
scalar/nullable/struct unboxing, shallow reference-bearing copies, independent and
unstarted cursors, retained versus cleared Current, automatic versus caller disposal,
collection terminal callbacks and sizing faults, Select behavior, fresh empty arrays
and Alchemy's read-only base-to-derived shape. Additional pairs cover every source
fault mode and all projection/count modes. Native checks retain O0/O2, ASan/UBSan,
forced collection, exact cleanup, 2,048-byte frames and 30-second subprocess caps.

Initial translation identifies existing bounded-code requirements: use an explicit
Exception catch rather than an Object catch-all, materialize generic results through
a setter before storing heap fields, and assign out parameters inside individual
blocks. These are managed adaptations; compiler guards remain unchanged.
The first native run passes 9/11 checks and exposes pre-start Dispose failing to stop
a later MoveNext. The corrected state transition and expanded count/fault cases are
recorded in artifacts/alchemy-linq-cast/tests-expanded.log.

Review checks upstream branch order, both lifecycle models, fault/disposal ordering,
negative and inaccurate counts, allocation-before-enumeration, copied value roots,
query reuse and unchanged compiler/runtime boundaries. Full engine, unchanged
fingerprint execution and saved-game replay remain unverified.

All 15 expanded Cast checks pass in 126.627 seconds: 4,998 repeated native/.NET
comparisons from 17 probes plus 384 comparisons covering explicit fault/size modes,
for 5,382 total. All 25 selected OfType, materialization and public-baseline checks
pass in 82.047 seconds. Total validation is 40 distinct checks; the baseline remains
46 native controls and one expected diagnostic gap. Main 48ecf14 remains an ancestor.
Evidence: artifacts/alchemy-linq-cast/{tests-expanded,regressions-baseline}.log.
