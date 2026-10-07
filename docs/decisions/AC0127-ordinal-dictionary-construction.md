# ADR AC0127: Explicit ordinal string-key dictionary construction

Recorded 2026-10-07 for AC3.22.

After committed AC5b.14 (48cf6f8), frozen discovery reaches
Dictionary<string, EffectOperationDefinition>(StringComparer.Ordinal) in
EffectOperationRegistry.Register IL_0005 after 266,560 metadata units.
StateLookup remains eight methods/9,921 units. Evidence:
artifacts/alchemy-hidden-value-generics-{refresh,engine}; no engine execution.

Generalize the existing ordinal set-constructor discovery into a shared ordinal
collection bridge. Keep exact selected owners, generic arity/parameter order,
constructor/helper signatures and direct selected getter proof. Give dictionary
construction its own DictionaryOrdinalConstruction grant. Retain getter evaluation
before the managed factory and reuse existing ordinal string-key behavior.

The admitted dictionary surface cannot expose a comparer or perform copy
construction; both remain diagnostic and must retain explicit comparer identity
when implemented. No marker is needed for existing dictionary operations. The
HashSet marker and copy-order behavior from AC3.21 are unchanged. Null, aliased,
joined, custom, lookalike-getter and non-string-key comparer calls remain rejected.
Integer CIL v50 records admission; object IR v38/native v65/layout v24 and all
production limits are unchanged.

Four probes cover exact UTF-16/case-sensitive keys, insert/overwrite/remove,
ordered enumeration, null/duplicate-key faults, retained key identity, hidden
readonly reference-bearing values and iterator versions. Grant removal denies
the exact dictionary factory while the set grant remains present. All 12 focused
dictionary/set checks and all 46 selected dictionary/public-baseline regressions
pass (58 distinct checks). Four new probes supply 1,176 repeated O0/O2 native/.NET
comparisons with forced GC and ASan/UBSan. The 2,048-byte frame and 30-second
subprocess bounds remain. The public baseline remains 43 controls/four gaps.

Review checks both generic arities, exact grants, retained getter evaluation,
existing null/key/version behavior and traced values through collection clears.
Main 48ecf14 remains an ancestor. Application rules/frozen inputs are unchanged.
Full engine compilation/execution and AC9 saved-game replay remain unfinished.

Evidence: artifacts/alchemy-ordinal-dictionary/tests-{a,b}.log and hashes.json.
Core: 2ce587d7154d4e19fb1b107e2247c70319b299b86fb9f69fb324db97fae42e65.
Manifest: 58e65d8dd8295258885e1eb5d10b71a261d6d5a430d6873ab9243aceb2fb9e86.
Fixture: 3e502dbff5836d56b891d44b0890c57364da2fa2b1b504b6b7799b388bae5a11.
