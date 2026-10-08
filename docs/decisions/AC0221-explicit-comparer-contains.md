# ADR AC0221: Explicit-comparer sequence membership

Recorded 2026-10-08 for AC8.77 after AC8.76.

Frozen ForcedMovementOperation.OneOf uses Enumerable.Contains with StringComparer.Ordinal.
Add the three-argument overload to the existing owned adapter, reusing AC8.71
SequenceComparison storage and equality helpers. Pinned Contains.cs and
MemoryExtensions.IndexOf establish element-first copied comparisons, explicit-null
default equality and exact array/List storage behavior. Do not delegate this overload
to ICollection.Contains or read arbitrary collection Count.

Exact arrays and Lists expose their original rooted backing array and count; callback
mutation observes in-place writes, but growth cannot switch the retained storage.
Other source shapes use ordinary cursors with early exit and finally disposal.
Both branches pass copied elements to the existing equality helper. Since its null
branch is reachable, admitted default-key semantics are required even with a nonnull
comparer. Nullable and broader reference equality are not added. No compiler/runtime,
profile, grant or production-budget changes (CIL106/IR63/interface20/native79).

## Validation and review

Snapshot artifacts/alchemy-contains-comparer is fced553 plus this slice. Ten roots
cover explicit/null/ordinal/ignore-case comparison, collection shortcut differences,
throwing Count, cursor/comparer failures and disposal, List growth/clear and array
mutation during allocating callbacks, mutable value equality, Integer64 and null
source. 10 x 32 x 6 x 3 repeats x O0/O2 = 11,520 new comparisons. The old explicit
comparer negative becomes an O0/O2 positive with exact singleton roots (294 more).

The broad ContainsComparer/LinqContains/SequenceEqual run passed 37 of 38 checks in
177.375s. Its sole failure was a stale negative for reference OfType.Select, already
admitted by previous compiler work. Replacing that negative with an exact two-cache
root positive passed both O0/O2 checks in 14.623s (720 comparisons). Thus 39 distinct
final checks passed across these runs, not in one clean aggregate rerun. New evidence
totals 12,534 comparisons. Native receipts are in native-evidence and
projection-native-evidence; logs are tests.log and projection-tests.log. All native
runs retain sanitizers, forced collection, exact root/borrow cleanup, exact/one-short
frames and 2KiB native frame warnings. Nullable and direct Ordinal-only HashSet
construction negatives remain precise; no wider HashSet constructor claim.

Review checked pinned comparison direction, copied values, null-source precedence,
explicit-null behavior, exact-type storage admission, retained old backing storage,
callback allocation roots, cursor finally disposal and unchanged two-argument logic.
No application input edits or compiler fallback. Production source/test bytes match
the validated snapshot. Existing unrelated fixture warning remains unchanged.

## Frozen audit

All six input hashes match inputs-verified.json. Core SHA256
 a88b4efa35c70d2f81dc4ffe37ebd4f08294c7d094730548a036e39e2be331be;
manifest 31c02d2d557fdd9811c6827f91997bf8cfa0d341d2055276eb240fbf61cec962.
The unchanged diagnostic allowances now reach CIL3010 missing Join at
Alchemy.Engine.Effects.ForcedMovementOperation.Encode, method0x06000cb6 IL00ac,
member0x0a000523, requested by Run IL0578. Engine work is 3,306,855 units/17,362ms.
StateLookup validates 15 methods/11,730 units/437ms. Both Emission fields are null;
there is no native engine emission, compilation or execution. Evidence is in
artifacts/alchemy-contains-comparer-refresh and -engine.

Next inspect the exact String.Join overload. The reduced non-sealed SortedDictionary
value duplicate-text gap remains open independently. AC9 full closure and saved-game
replay are unfinished.
