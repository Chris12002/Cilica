# ADR AC0068: Default-equality Contains and primitive reference operands

Accepted 2026-10-07 for AC6.12.

The two-argument Enumerable.Contains now uses the pinned .NET 10.0.11 order:
ICollection<T>.Contains, an owned query shortcut, then ordinary enumeration with
null-source validation, short circuit and finally disposal. Existing exact key
helpers provide admitted equality. Ordinary enumeration copies a value candidate
before Equals; array collection search preserves its existing borrowed receiver.

The owned query contracts preserve observable optimizations. Ordered and default
Distinct search their source without key/sort/hash work. ConcatN searches newest
heads before its two-source tail. Where and OfType scan independently of their
query cursor. Array/IList/Where Select chains use fresh fused traversal; general
and other iterator Select chains consume their initial cursor as the pinned runtime
does. These contracts are owned implementation details, not framework APIs.

A reduced regression exposed closed generic String/Object type-test operands
arriving as primitive signatures. ReferenceOperand now resolves those exact selected
runtime identities, reusing existing reference type-test/cast lowering. Nulls,
mismatches, cast faults and identity are covered separately. No value-box admission,
intrinsic grants, IR/native profiles or resource limits change.

Seventeen Contains probes pass 12,240 repeated native/.NET comparisons at O0/O2;
the two reduced reference-operand probes add 588. Checks cover null/search values,
UTF-16, callback/fault order, custom collection dispatch, cursor reuse/active cursors,
projections, mutating/allocating struct equality and cleanup under forced GC and
ASan/UBSan. No-cache probes require exact cleanup; OrderBy's one retained comparer
is counted explicitly, and probes with compiler-generated caches require stable
retention. The native frame bound remains 2,048 bytes and subprocess bound 30 seconds.
All 17 slice checks and 49 adjacent boxed-type/OfType/Select/public-baseline checks
pass (66 distinct selected checks). Evidence: artifacts/alchemy-contains/ac6l3-*,
ac6l4-select-tests.log and ac6l5-*. Review checked source algorithms, copied equality
receivers, disposal on early return/fault and fresh versus consuming traversal.

The comparer overload, nullable equality, general reference equality, boxed
collection factories and the existing reference OfType.Select variant-dispatch
composition remain diagnostic. The reference projection failure is explicitly
retained as a negative test; value-type projection has native evidence.

Frozen EngineStep advances past Contains to the 256 structural-snapshot limit at
BorrowedShieldPayload.IsLegalSelection IL_0035: 25 value roots, 197 reference roots,
8 vector roots and 42 snapshots outside the value/reference roots. StateLookup
still reports 257/256 discovered identities. These are partial discovery counts,
not measured emitted runtime layouts. Full engine/replay remains unverified.
Evidence: artifacts/alchemy-contains/final/engine-and-state.json. The public baseline
stays at 31 native controls/16 gaps. Core SHA-256:
9f0f67ed78737d3f7843fc4e20b3cc95b41e6e17379e3b5b04a3765e1ec0cef4;
manifest: 11740ad2989d696a28a2e19feda3e83689170b3c137dd41b839ca06d83a2c879.
