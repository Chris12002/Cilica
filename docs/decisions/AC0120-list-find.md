# ADR AC0120: List predicate lookup

Recorded 2026-10-07 for AC3.20.

After committed AC5b.13 (66d02e6), the unchanged diagnostic copy reaches
List.Find in SpellResourceAdmission::<Evaluate>g__Assign|7_2 IL_00c8 through
NormalSpellDraftAdmission.Evaluate. Discovery charges 233,697 metadata units.
StateLookup remains eight methods/9,921 units. No engine emission/execution.
Evidence: artifacts/alchemy-hidden-enum-generics-{refresh,engine}.

Add the pinned managed Find body to the owned List<T>. Check a null predicate
before inspecting size, scan the live size/backing storage, and reread items[i]
after a successful callback. Return default(T) when no match exists. Capturing
size or returning the predicate's copied argument would change mutation behavior.
Existing typed delegates, value returns and rooting compile this ordinary body;
no compiler/native profile, layout, grant or resource-limit change is needed.

Four probes compare scalar, reference and reference-bearing struct results across
nine mutation modes and ten search values. They exercise live growth/shrinkage,
clear/remove/replace, returned-element rereads, missing/default results, callback
faults and null receiver/predicate behavior, including null predicates on empty
lists and null receivers. These provide 2,160 repeated O0/O2 native/.NET
comparisons. All 33 selected checks pass; all 15 focused/public-baseline checks
also pass after strengthening fault order. Forced GC, ASan/UBSan, 2,048-byte
frame warnings and 30-second subprocess caps remain unchanged. The public
baseline remains 43 controls/four gaps.

Review checks the adaptation against pinned upstream List.cs, reloading after
callbacks, argument/fault order and roots through allocation pressure. Main
48ecf14 remains an ancestor. Frozen inputs and application rules are unchanged.
Full engine compilation/execution and AC9 saved-game replay remain unfinished.

Evidence: artifacts/alchemy-list-find/tests-{a,b}.log and hashes.json.
Core: 40f3806c722e3edb8e087cab6d3aedbb2c0c708f4dd8d72600e29b3fa5256a14.
Manifest: 9e2e82e56fe6b596ec980a97ae63c2c6d63a53ebef5c5ec81fd05d26f3dd6084.
Fixture: 4dc0d2fc4cf0f2001cfc191c74747ce532b1d1d8eb3ff1459d611b8998fa65fb.
