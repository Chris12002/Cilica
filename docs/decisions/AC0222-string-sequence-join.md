# ADR AC0222: Join string sequences

Recorded 2026-10-08 for AC8.78 after AC8.77.

Frozen ForcedMovementOperation.Encode calls String.Join(string,IEnumerable<string>)
at IL00ac and IL00ed. Its inspected IL is retained in alchemy-string-join/encode-il.json.
Implement that exact overload in the existing StringConcatenation source, using
pinned String.Manipulation.cs for dispatch, identity, validation and cursor ordering.
No new source-list entries, grants, profiles, compiler or runtime changes.

Like the pinned implementation, Lists (including derived Lists) bypass overridden
interface enumeration, followed by string arrays. QueryStorage captures List storage
and count. The helper calculates separator/product and string totals using Int64,
checks the supported Int32 range, allocates an exact managed UTF-16 buffer and copies
checked code units. Preserve empty and singleton identity. Defensive snapshot/retry
protects sizing/copy disagreement; concurrent mutation remains unvalidated in the
single-threaded execution profile.

Other sequences acquire one cursor, move once, read Current, then move again before
constructing the result. Empty/singleton results return immediately through disposal.
For multiple values, append the first value, then separator before each next Current,
and dispose through finally. The existing owned chunk builder replaces pinned
ValueStringBuilder; Int64 length checks precede each append. Null separator/entries
contribute no characters, but separators still separate every position. Generic,
Char, string-array-specific, object and span Join overloads are not added.

## Validation and review

Isolated snapshot artifacts/alchemy-string-join starts at 30d9d4b. Seven roots x 32 x
4 pairs x 3 repeats x O0/O2 give 5,376 new native/.NET comparisons. Cover array/List
shortcuts (including a derived List whose interface enumeration throws), null/empty
entries and separators, NUL and lone surrogates, single-item and multi-item identity,
all cursor fault phases and disposing faults, allocating projections, buffer growth
and null-source messages. Generic/Char overloads remain precise negatives.

Initial adjacent run: 20 checks, 18 pass and two new native checks fail because the
test expected one cached projection delegate. Inspected fixture IL instead constructs
a fresh delegate per call. Correct expected static roots to zero and strengthen
multi-item identity coverage. The focused five-check rerun passes in 40.028s, including
both native optimization levels; adjacent checks are unchanged. Thus 20 distinct
checks pass across runs, not one clean aggregate rerun. Final receipts are in
final-native-evidence, log fixed-tests.log; the initial log is tests.log (54.996s).
ASan/UBSan, forced collection, exact roots/borrows, exact/one-short frames and 2KiB
native frame warnings remain enabled. Existing unrelated fixture warning is unchanged.

Review checks pinned branch order, subclass shortcut behavior, first/second MoveNext
and Current order, null handling, identity, finally disposal, bounded length/copy
arithmetic, reference rooting through allocation and unchanged Concat behavior.
Production source/test bytes match the validated snapshot. No game code changes.

## Frozen audit

All six input hashes match inputs-verified.json. Core SHA256
519ac5e589cd1ea0661b5736c4217d4b0cd75332c21321535057903c0ddf75bd;
manifest b3ac2f01dcd30defaba650d7924992d6e203f3ace5d231256a955f6cb982e095.
Unchanged diagnostic allowances reach CIL3010 missing Replace in
Alchemy.Engine.Effects.SuppressTaggedEffectOperation.Label, method0x06000f8c IL00b7,
member0x0a0005e6, requested by Run IL018e. Engine work is 3,310,764 units/17,648ms.
StateLookup validates 15 methods/11,736 units/439ms. Both Emission fields are null;
no engine C++ emission, compilation or execution. Refresh/engine receipts are in
artifacts/alchemy-string-join-refresh and -engine. CIL106/IR63/interface20/native79
and production budgets remain unchanged.

Next inspect Replace's exact overload. The separately reduced non-sealed
SortedDictionary duplicate-text gap and AC9 full closure/saved-game replay remain open.
