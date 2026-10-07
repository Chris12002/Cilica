# ADR AC0108: List predicate existence

Recorded 2026-10-07 for AC3.17.

After committed AC3.16 (6bbe19a), the unchanged diagnostic copy reaches
List<SpellEffectIdentity>.Exists in SpellComponentState::.ctor IL_0145 through
BeginSpell/CreateHandlers. It charges 154,257 metadata units. StateLookup still
translates eight methods, now charging 9,919 units after the library declaration
change. No native engine emission/execution is attempted. Evidence is archived in
artifacts/alchemy-dictionary-capacity-{refresh,engine}.

Implement the pinned one-line composition FindIndex(match) != -1. Existing
FindIndex retains its argument checks, captured end index and live backing-array
reads; no second search algorithm or enumerator is introduced. Compiler/native
profiles, grants, layout and resource limits are unchanged.

Four probes provide 2,160 repeated O0/O2 native/.NET comparisons across nine
mutation modes and ten search values. They cover scalar, traced-reference and
reference-bearing struct lists, early and absent matches, null receiver/predicate,
empty lists, callback faults, add/clear/remove/insert, capacity replacement and
changed elements. All 25 selected checks pass, including the ten existing
FindIndex checks and seven baseline checks. The public baseline remains 43 native
controls/four gaps. Native tests retain forced GC, ASan/UBSan, 2,048-byte frames
and 30-second process caps.

Review confirms the exact pinned composition, reuse of existing null/bounds/fault
order, copied predicate arguments and roots during callbacks. No application rules
or frozen inputs changed. Local main 48ecf14 remains an ancestor. Full engine
compilation/execution and AC9 saved-game replay remain unfinished.

Evidence: artifacts/alchemy-list-exists/tests-a.log and hashes.json.
Core: d77f711997696db4e72d687ba835aed4eccd83fbd606ddfbae60911e3447b569.
Manifest: dd6abbbaa6d0e3a0f7e475da941963ac3f5f23605c6151ac47602a74ae6601fc.
Fixture: c0940da0f607bb0b672a92e5a14c885d0270c7c15937b34ff624745ea8d55053.
