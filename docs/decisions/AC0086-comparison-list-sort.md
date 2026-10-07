# ADR AC0086: Comparison-based List sorting

Accepted 2026-10-07 for AC3.10.

The isolated AC8.23 capacity probe identified List<SpellSourceGrantState>.Sort
(Comparison<T>) and three equivalent state-record sites in frozen GameState
construction. Adapt pinned ArraySortHelper's comparison-based introsort to a
captured T[] prefix. Preserve threshold 16, median-of-three partitioning, right
recursion, insertion/heapsort fallbacks, comparison sequence and partial stores.
Keep this physical-element algorithm separate from LINQ's stable index-map sort;
its backing-array mutations are observable to comparison callbacks.

List checks comparison for null before testing size and increments version only
after successful sorting, including empty/singleton cases. Callback exceptions
retain InvalidOperationException wrapping and inner identity; IndexOutOfRange
becomes the pinned inconsistent-comparer ArgumentException, even when deliberately
thrown by the callback. Explicit partition checks preserve span boundaries within
larger backing arrays. Capacity changes in callbacks leave the sort working on its
captured old storage. All value/reference locals retain ordinary precise rooting.

The error resource needs a closed Comparison<T> name. General Type.ToString is
unsupported, so a private parameterless ComparisonSort<T>.ComparisonName helper
requires its exact SortComparisonName grant and same closed-owner caller. Discovery
validates the ordinary delegate shape and takes its bounded RuntimeName, charging
existing aggregate literal units. Translation emits an existing StringLiteral with
normal allocation/fault/root checks. Both ordinary literal validators and native
literal quotas still apply. No runtime reflection or new native operation is added.
Integer CIL becomes v25; native v57, object IR v30 and layout v22 remain unchanged.

Reentrant Clear revealed a preexisting observable mismatch: the owned List zeroed
all element shapes. Pinned .NET clears only reference-bearing elements. Clear and
RemoveAt now use the existing granted ContainsReferences classification, preserving
scalar backing slots while still releasing references, including those inside
values. Sorting while clearing/removing is the reduced regression. No game code,
resource bound or unsupported gate was changed.

Ten probes pass 3,324 repeated native/.NET comparisons: 72 integer length/pattern
pairs, two organ-pipe stress sizes, 162 fault-position/type pairs, inconsistent
comparisons, six reentrant operations, validation, reference/value sorting and
exact nested closed-type fault text. Callback allocation exercises forced GC.
Native O0/O2 builds keep ASan/UBSan, exact cleanup, 2,048-byte frame warnings and
30-second subprocess caps. The initial O0 stress repeated effectively identical
512/1,024-item cases 49 times and timed out; it now retains both distinct cases.

All 34 selected checks pass across final focused runs: list mutation and the
39-control/8-gap baseline in ac3-10c-tests.log, reentrant/validation in d, and
updated grant/access checks in e. The c run's two failures came from a static
lambda cache violating the test's zero-static-root assumption; the validation
fixture now allocates an explicit callback owner. The d run's remaining assertion
expected a later private-helper check, but foreign callers correctly fail earlier
at lexical type access; e pins that actual diagnostic. Initial a/b logs retain the
unsupported Type.ToString finding and scalar-clear mismatch. No validator was
relaxed. Parameterless/comparer/section sorting and Array.Sort remain unsupported.

Review checked finite recursion and arithmetic, span-relative bounds, captured
array lifetime, value copies and rooting across callbacks, mutation/version/fault
order, exact helper grants, argument access, literal budgets and unchanged default
Sort diagnostics. Frozen inputs are unchanged. Production engine/state still hit
256-type bounds; engine has 5,381 metadata work units left at that point. In the
isolated 1,024-type copy, EngineStep advances past GameState sorting to
ApplyFallingUnconscious IL_0008 and exhausts metadata work while reading generic
constraints. StateLookup still translates eight methods. There is no engine native
emission/execution from that experiment; full saved-game replay is unfinished.

Evidence: artifacts/alchemy-list-sort/ac3-10a-* through ac3-10e-*,
engine-and-state.json, capacity/type1024.json and capacity/source-hashes.json.
Core: 71f2ecc38cfc28f9c12678228ec59ec3d547d7dc0768c8cde5575db84230dabd.
Manifest: c4648791f3807390973ff9e22f57d902cda27d520d2bf3e7022e630a6652dcfd.
