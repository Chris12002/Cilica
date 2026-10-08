# ADR AC0197: Enumerable set union

Recorded 2026-10-08 for AC3.33, following AC8.62.

Frozen ResolveChain token 0x060010ae calls HashSet<PlayerId>.UnionWith at IL_06e7
for family knockout protection. Follow pinned HashSet.cs: null other raises
ArgumentNullException, then foreach inserts through existing SetStorage.Add.
No Count/CopyTo preflight, self shortcut, new intrinsic or game change. Existing
storage owns resize/version/callback behavior; managed foreach owns disposal.
Other set algebra remains unsupported.

All 67 selected union/basic/enumeration/construction/ordinal-source checks pass
in 304.303 seconds. Twelve new probes compare 72 input pairs at three repeats
and O0/O2 (5184 comparisons), covering Int32/Int64/nested values/ordinal strings,
retained first identity, null order, sparse storage/resizes, self-union (including
hash/equality callbacks), ISet dispatch, source faults and overriding disposal,
partial mutation and ICollection Count avoidance. ASan/UBSan, forced GC, exact
roots/cleanup, frame exhaustion and subprocess bounds remain enabled. Initial
union checks found only an omitted expected ordinal-comparer singleton in test
cleanup accounting; outputs matched. Corrected accounting passes both modes.

Review checked the pinned single enumeration algorithm, storage callback copies,
mutation before failure, finally disposal and exact static root counts. The tested
snapshot is 5905742 plus these owned source changes, CIL88/IR57/native78. All 85
pinned sources and six frozen inputs verify. No limits changed. Local main
48ecf14 remains an ancestor. Concurrent exact netstandard2.0 test-contract work
is excluded from this immutable test/probe snapshot.

Evidence: artifacts/alchemy-set-union{,-refresh,-engine}.
Core 0949180ad3a6d266ca2eae2fe5cde347c1e4eff72beae1bfb50cbb3ca44aca8f.
Manifest d6eb1cf88afe8e121623233b8624366ce3b4e8306c27ff9ba55479c95b9d4af0.
Frozen discovery next rejects protected abstract GameOccurrence.WriteContext,
token 0x06000ce5, requested from WriteCanonical 0x06000ce4 IL_0078. It charges
1236925 metadata units/4570 ms. StateLookup translates 15 methods/11616 units/
449 ms. No engine code is emitted or executed; full engine, unchanged fingerprint
and saved-game replay remain unfinished.
