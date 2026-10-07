# ADR AC0128: Indexed Where

Recorded 2026-10-07 for AC6.16.

After committed AC3.22 (b88608c), frozen discovery reaches indexed
Enumerable.Where<SpellComponentState> in SummonComposition.Parameters IL_004a
after 279,898 metadata units. StateLookup remains eight methods/9,921 units.
Evidence: artifacts/alchemy-ordinal-dictionary-{refresh,engine}; no engine execution.

Add the exact managed overload and a separate owned IndexedWhereIterator<T>.
Validate source then predicate eagerly and return the selected cached empty array
for an empty array input. Otherwise defer GetEnumerator until first MoveNext and
use ordinary source enumeration, including List version checks. Advance a checked
Int32 index for every input element before invoking the predicate. Do not combine
predicates or advertise the optimized nonindexed query traversal contracts.

The owned state machine follows the pinned Where yield algorithm. It retains
Current after completion and disposal. Fault cleanup uses finally, avoiding a
catch handler that would alter future search-before-unwind behavior. General
side-effecting exception filters retain the existing compiler diagnostic.

The installed .NET 10.0.11 oracle's generated Dispose clears the source cursor
and resets state to -2 after successful disposal, permitting subsequent reuse.
Normal completion instead leaves state -1. Terminate before calling user Dispose,
so a disposal fault is not retried by fault cleanup. Oracle IL is archived in
artifacts/alchemy-indexed-where/oracle-{dispose,move}-il.txt. This is measured .NET
10 behavior, not a promise about every framework/compiler version's private
iterator layout. No application iterator state-machine translation is changed.

Nine probes cover deferred access, independent cursors, index/predicate order,
terminal traversal, array/list mutations, source/predicate/disposal failures,
null ordering, reference-bearing values, retained Current and fault/disposal
reuse. They supply 2,970 repeated O0/O2 native/.NET comparisons. Two native GC
checks measure eight live objects before disposal, seven after clearing the source
cursor (including the retained old Current), and one emergency exception after
the query dies. Forced GC, ASan/UBSan, 2,048-byte frame warnings and 30-second
subprocess caps remain enabled. All 20 focused checks and 42 selected Where/Select/public-baseline regressions pass (62 distinct checks). The public baseline remains 43 controls/four gaps.

Review checked source acquisition before active cleanup, termination before user
disposal, normal-completion versus explicit-disposal identity, checked arithmetic,
rooted generic Current and preservation of the existing nonindexed iterator.
No grants, compiler/IR/native profiles or production limits change. Main 48ecf14
remains an ancestor. Full engine execution and saved-game replay remain unfinished.

Core: 705f740082ac66c6145dfa33a2b786fbe2b10e3df878608b939259d51f04e11b.
Manifest: 57fa4834fe127bd8dcb6b94bd204e2bd4b5adb7d188298ec77f0bf6327baf4d8.
Fixture: 5000e7cb9a417109fb8586c497543043685af1461f2abb5a295122acc6aeb166.
