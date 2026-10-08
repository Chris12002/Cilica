# ADR AC0224: Predicate removal from lists

Recorded 2026-10-08 for AC8.80 after AC8.79.

Frozen ObstacleOverflow.Drain calls List<T>.RemoveAll. Adapt the pinned List.cs
algorithm in the existing owned List source, preserving live size and backing-storage
reads through callbacks. Find the first removed element, then compact retained values
in order. Predicate arguments are copied values. Do not snapshot the size/storage or
add an enumerator-version guard: callbacks can change the list and pinned semantics
must remain observable. If a predicate throws, already copied elements remain changed
while this invocation has not published a new size or incremented the version.

For reference-bearing T, clear the vacated tail through existing ArraySections<T>.
ClearSection, retaining range validation even when reentrant mutation makes the
computed length negative. A typed loop with no validation would silently miss that
exception. Scalar backing slots remain unchanged. Only after clearing succeeds,
publish the compacted size and increment the version once. No-match/empty returns
zero without a version change. Null predicates throw with parameter match even on
empty lists; null receivers retain normal managed call semantics.

Compiler, runtime, profiles, grants and production budgets are unchanged
(CIL106/IR63/interface20/native79). No game-specific code or approximation is added.

## Validation and review

Isolated snapshot artifacts/alchemy-list-remove-all starts at c64440f. All 19
focused and adjacent ListMutation checks pass in 56.023s. Eight new roots x 16 x 6
argument pairs x 3 repeats x O0/O2 produce 4,608 new native/.NET comparisons. Cover
empty/no-match/all/partial removal, stable compaction, live list growth/clear/edit/
shrink, reentrant no-op removal, predicate faults after copies, cursor version after
success/failure, strings, references and reference-bearing copied values, null
receiver/predicate and allocation pressure. Exact error messages include the
reference-tail clear range fault after callback Clear.

Four additional native lifetime checks hold the list rooted while removing first
one then all referenced objects. Exact heap counts drop by one object at each step
for both reference elements and structs containing references, at O0/O2. Repeated
empty removal does not retain objects; dropping the list leaves only the emergency
exception and no frames/borrows. New differential receipts are in native-evidence;
tests.log records all 19 checks. Sanitizers, forced collection, exact root/borrow
cleanup, exact/one-short frames and 2KiB frame warnings remain enabled. Build has
only the existing unrelated fixture warning.

Review compared compaction/callback/clear/publication order with pinned source,
including negative clear length after reentrancy, typed default stores, copied
predicate values, persistent list roots through allocation and no-match version
behavior. Source/test bytes match the tested snapshot. No input assemblies changed.

## Frozen audit

All six immutable input hashes match inputs-verified.json. Core SHA256
505e7e179441349739059e7f20c5f039725b0e25222f9d93ae585b8eb84e8604;
manifest 347b83f0671b0a552c50df733f951dc9af993776a9f0062cea30664993ee29b2.
Unchanged diagnostic allowances reach the separately reduced non-sealed reference
formatting gap: CIL2001 in SortedDictionary.Add method0x06000525 IL0033, specialization
e26ed9bab3e9c2aac31b43fc7d12f4003ebe284c5fd3a3e29849974663ce356e. Reachable through
DispelOperation.Run -> Collect -> <Collect>g__Offer|10_0. DictionaryKeyText requires
an exact sealed ordinary reference, but this duplicate pair's value is List<string>.
Engine work is 3,387,734 units/17,668ms; StateLookup validates 15 methods/11,742
units/440ms. Both Emission fields are null; no engine emission/compilation/execution.
Receipts: artifacts/alchemy-list-remove-all, -refresh and -engine. Next work must
preserve actual runtime ToString dispatch rather than substituting a static type
name for non-sealed values. AC9 full engine closure and replay remain unfinished.
