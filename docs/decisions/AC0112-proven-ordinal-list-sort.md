# ADR AC0112: Proven ordinal string List.Sort

Recorded 2026-10-07 for AC3.18.

## Decision

After d40ed38, frozen engine discovery reaches EffectTags.Normalize IL_00a7:
List<string>.Sort(IComparer<string>) immediately consumes StringComparer.Ordinal.
The unchanged diagnostic copy charges 184,827 metadata units. StateLookup remains
eight methods/9,919 units. No engine emission/execution. Evidence:
artifacts/alchemy-wide-delegates-{refresh,engine}, including normalize-il.txt.

Map the exact selected Sort(IComparer<T>) declaration only for List<string>
callvirt operations. A new ListOrdinalSort grant authenticates the selected
managed SortOrdinal bridge. Discovery validates source/bridge/getter signatures,
visibility, implementation flags and closed ownership. Translation consumes the
original comparer argument only when its same-block expression is the exact
selected StringComparer.Ordinal getter. Retain exact getter and list identities
in the bridge proof. Getter evaluation and receiver null-check order remain intact.
Null/custom/aliased comparers, joins, lookalike getters and nonstring lists fail
with actionable diagnostics rather than assuming culture-dependent default order.

The bridge sorts the captured items prefix through the existing ComparisonSort
introsort and a static ordinal comparison, then increments version. It retains the
zero/singleton version behavior and normal object identity/rooting. No comparer
callback from application code is admitted; ordinal comparison cannot produce the
inconsistent-comparer callback faults of the generic overload. Its null-string and
UTF-16 ordering comes from the existing String.CompareOrdinal implementation.

Integer CIL v38 records the proof extension. Native v64, layout v24 and existing
IR operations are unchanged; no resource limit increase. Core provenance records
the pinned List/ArraySortHelper adaptation. Parameterless/section/custom/default
comparer sorting and Array.Sort are not implemented by this slice.

## Validation and review

Four probes provide 1,170 repeated O0/O2 native/.NET comparisons: 48 length/pattern
cases across insertion/introsort boundaries, null/empty/duplicate strings, embedded
NUL, combining characters, surrogate pairs, ordinal BMP order, distinct equal-string
identities, version invalidation and null receiver faults. Negative cases reject
null, local aliases, custom comparers, a different owner's get_Ordinal, joins and
nonstring lists. Removing the exact grant denies compilation.

All 37 distinct selected checks pass: six focused checks, 32 checks including one
repeated negative test plus comparison-sort/string/baseline regressions, then all
six focused checks after review tightened exact bridge-owner validation. Existing
ASan/UBSan, forced GC, 2,048-byte frames and 30-second subprocess caps remain.
Baseline stays 43 native controls/four gaps. Review checked exact identity proof,
original evaluation order, list version/storage semantics, lack of custom comparer
reachability, helper authorization and aggregate metadata/argument accounting.
No outstanding finding. Main 48ecf14 remains an ancestor; frozen inputs unchanged.

Full engine/replay remains unfinished.
Evidence: artifacts/alchemy-ordinal-list-sort/tests-{a,b,c}.log and hashes.json.
Core: 44982fc144c46c108640a147efd5aa27cb9fc834906c86429ae7819765616de5.
Manifest: 59f40c837d8d87d11ccf96c719552342f438c83260829d165957c3ccde23321d.
Fixture: c0c93b0329393aec89c590dd6fa8672819c53ab92ff14173928f2269afeb4fb0.
