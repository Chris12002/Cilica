# ADR AC0178: List reversal

Recorded 2026-10-08 for AC3.31.

The unchanged ContinueShieldWave reverses List<PendingOccurrence>. Add Reverse()
and its index/count implementation from the pinned List.cs contract. Validate
negative index before negative count, then size-minus-index against count. Preserve
exact exception messages/parameter names. Replace the private Array.Reverse span
helper with ordinary typed managed swaps after full validation; keep count,
capacity and backing-array identity. Increment version once on every successful
call, including empty/singleton ranges; errors leave contents and version intact.
No public Array.Reverse mapping or native intrinsic is added.

Nine probes supply 2,952 repeated O0/O2 native/.NET comparisons, including a 100-pair
range/error matrix, whole/partial/double reversal, empty ranges, concrete/boxed
cursor invalidation, exact error text, null receivers, read-only views, reference
identity, nullable/wide values, reference-bearing value copies and active Select
cursors. Forced GC, ASan/UBSan and exact frame/cleanup limits remain enabled. All
18 focused and 16 list-mutation/public-baseline checks pass (34 distinct checks).
An initial fixture used unsupported ArgumentOutOfRangeException.ActualValue;
that unrelated accessor was removed while full Message and ParamName checks remain.

Review checked subtraction/addition bounds, no callbacks/allocations during swaps,
typed temporary rooting, preserved backing-store identity and success-only version
updates. Profiles, grants and production/diagnostic limits are unchanged. Main
48ecf14 remains an ancestor. Evidence: artifacts/alchemy-list-reverse{,-refresh,-engine}.

Frozen discovery next reports unmapped StringSplitOptions in String.Split(char,
StringSplitOptions), DamageReplacementCheckpoint.RecordAnswers IL_0039: 989,604
charged metadata units in 4,148 ms. StateLookup remains 15 methods/11,529 units
(448 ms). Exact caller IL is archived. Full engine, unchanged fingerprint and
saved-game replay execution remain unfinished.
