# ADR AC0160: Private static fields from containing-type nests

Recorded 2026-10-08 for AC8.48.

## Evidence and decision

The measured 1,572,864-unit diagnostic copy reaches
AlchemyGameEngine/AlchemyResolutionAdapter.Prepare IL_02ed, loading the private
Handlers static field on its enclosing AlchemyGameEngine. Discovery charges
1,100,871 units in 3,695 ms; StateLookup remains 15 methods/11,517 units.
Evidence: artifacts/alchemy-metadata-1572864/adapter-prepare-il.txt and work1572864.json.
The reduced Numbers probe reproduces the static access rejection before the fix.

Reuse ObjectLexicalAccess.WithinDeclaringType, already used for private method calls,
with an overload taking the target owner and lexical chain. The old method overload
forwards unchanged facts. ObjectStaticFieldAccess admits private fields only through
that helper in the integer-object profile. Require matching image identity, a valid
nonempty complete suffix in the caller's chain, empty owner specializations and zero
generic arities throughout. Discovery and logical/scalar validators share the rule.

Containment is directional; sibling, child, unrelated and foreign owners do not gain
access. PrivateScope remains forbidden. Existing same-owner/public/assembly rules and
readonly owning-initializer checks are unchanged. No new static addresses, lifetime
rules, physical layouts or resource bounds. CIL v71, object IR v45 and native v71
record the newly accepted programs; layout v26 and interface plan v16 stay unchanged.

## Validation and review

Six probes add 1,764 repeated native/.NET comparisons at O0/O2 for numeric/wide fields,
reference roots across allocations, reference-bearing value copies, readonly initialized
objects, deep nesting and retained initializer faults. Readonly roots are counted exactly;
fault retention is stable across repeats. ASan/UBSan, forced collection, exact cleanup,
2,048-byte frame checks and 30-second subprocess caps remain active.

The first run passes 66/86 checks; 19 new checks share a fixture that accidentally uses
ldsflda when clearing a static struct, and the legacy negative expects a later diagnostic
than the old profile's instruction rejection. The positive fixture now clears via a value
return, with the original address operation retained as a negative. The old-profile
assertion checks its exact instruction boundary. No compiler guard is relaxed for these.
A focused rerun of the new slice and static-value regressions passes all 46 checks in
17.272 seconds, giving 107 distinct passing checks across final results. Metadata tests
cover direction, matching foreign tokens, generic owners and old profiles. Both IR stages
reject missing chains, private-scope/family flags and invalid readonly writes. Existing
private-call, nested-cache and assembly-static regressions pass. Evidence:
artifacts/alchemy-nested-static-fields/{red,tests,reviewed-tests}.log.

Review checked complete suffix equality, nonempty/generic/image guards, the unchanged
private-method wrapper, private-scope ordering, readonly writes, static value expansion,
rooting and retained initialization failures. Full engine, unchanged fingerprint execution
and saved-game replay remain unverified.

All four public baseline checks pass in 69.530 seconds: 46 native controls and one
expected diagnostic gap, now on native v71. Total validation is 111 distinct checks.
Main 48ecf14fd7a2c72e63281b50a6a61ea1c090c187 remains an ancestor.
