# ADR AC0155: Source collection plus proven ordinal set construction

Recorded 2026-10-07 for AC3.27.

## Evidence and decision

The 1090e51 frozen engine with the measured diagnostic capacities reaches
HandLimitHandler.Resolve IL_00dd after 820,241 metadata units. The unmapped member
is HashSet<string>(IEnumerable<string>, IEqualityComparer<string>), immediately
preceded by the selected StringComparer.Ordinal getter. StateLookup remains
15 methods/11,511 units. This is discovery evidence, not engine execution.

Add the exact selected declaration and managed CreateOrdinal overload, with its
own HashSetOrdinalConstruction grant. The existing bridge authenticates a String
owner, IEnumerable<!0> source, IEqualityComparer<!0> final argument, exact helper
signature/flags and directly proven same-block selected Ordinal getter. The generic
factory path now accepts the authenticated two-argument reference packet as well
as the existing Int32 dictionary-capacity packet; each argument is independently
validated and popped against its type. Null/aliased/custom/joined/lookalike
comparers and non-string owners remain diagnostic.

The ordinary managed source constructor and new private ordinal source constructor
share CopyEnumerable(collection, comparerIdentity). Null validation, capacity
selection, foreach/disposal, duplicate filtering and construction trimming remain
managed. Reuse source-set slots only when its private comparer marker matches the
new set. Otherwise enumerate/rebuild, preserving the distinction between default
and explicit ordinal comparer identities. The existing default copy path passes
false and retains its behavior.

Integer CIL v70 records admission. The owned core and manifest change; interface
plan v16, object IR v44, layout v26 and native v70 are unchanged. Production limits
and measured diagnostic capacities remain unchanged. No application rule is edited.

## Validation and review

The reduced source constructor first reproduces CIL3010 in red.log. Seven probes
then provide 2,058 repeated native/.NET comparisons at O0/O2 with ASan/UBSan and
forced collection: null/empty/case-sensitive/exact UTF-16 keys, duplicates, retained
first string identity, source-set copies with matching/different comparer identity
after removals, null-source exception details, source-expression exceptions and
iteration callback exceptions. The source-expression test asserts exactly zero
retained roots when it throws before the getter, and exactly one singleton root
when the getter executes. Other new probes retain exactly one singleton root.

Both ordinal set constructor overloads are independently rejected when their
exact grant is removed while the other remains. Six unsupported comparer forms
remain diagnostic. All 39 ordinal set/dictionary/capacity checks pass. Another
30 shared set construction, enumeration and operation checks pass, with one
repeated translation check: 68 distinct focused checks overall. Evidence:
artifacts/alchemy-ordinal-set-source/{tests,regressions}.log. No costly rerun
followed an unexplained timeout; each native subprocess retains its 30-second cap.

Review checked arity before source-parameter inspection, nominal constructor/
helper identity, grant separation, typed argument order, getter proof, source-null
and iteration faults, matching/different comparer copy paths, allocation rooting
and unchanged native boundary ownership. Full engine, unchanged fingerprint
execution and saved-game replay remain unverified.

All four compatibility baseline checks also pass (46 native controls and one
expected diagnostic gap), bringing validation to 72 distinct checks. Main
48ecf14fd7a2c72e63281b50a6a61ea1c090c187 remains an ancestor of this branch.
