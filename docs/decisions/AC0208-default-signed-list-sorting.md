# ADR AC0208: Default signed integer list sorting

Recorded 2026-10-08 for AC3.36 after AC5b.16.

Frozen GameStateHasher.AppendSpell sorts a List<long> of negated component IDs.
Add parameterless List<T>.Sort using the existing managed ComparisonSort<T> and a
static copied-argument callback to ComparerOperations<T>.Compare(ref first, second).
The existing IntegerComparerCompare capability selects the exact owned numeric
CompareTo body. No comparer object/cache, new intrinsic, privilege, IR or native
runtime operation is needed. A delegate is created only when size exceeds one;
version increments after successful completion, including empty/single-item lists.
The existing pinned introsort, comparison-fault wrapping and array roots are reused.

The initial design tested all four integer comparer carriers. Int32 and Int64 pass;
UInt32 reaches the existing unsigned-delegate-parameter diagnostic, and UInt64
reaches the existing unsupported vector-storage diagnostic. Keep these as precise
negative tests and limit this slice's capability claim to signed 32/64-bit lists.
String/culture, enum, nullable defaults and the section overload also remain rejected.
No application code is changed to work around these boundaries.

The first 39-test run passed 35 checks in 113.270s; only the four unsigned native
cases failed at the two existing compiler boundaries. Replace those expectations
with two focused rejection tests; all six boundary cases pass in 3.111s. Together,
37 distinct final checks pass: 20 default-sort checks and 17 unchanged comparison/
ordinal controls. Five authored positive probes plus the promoted former default
sort probe x49 pairs x3 repeats xO0/O2 provide 1,764 new native/.NET comparisons.
Native tests use ASan/UBSan, forced GC, exact cleanup and bounded frame/subprocess
limits. Coverage includes signed extrema, wide high/low bits, duplicates, ascending/
descending patterns, multiple introsort sizes, repeated sorts/reversal, clear/reuse,
empty/single-item cursor invalidation and null receivers. The old private sort-name
access/grant checks remain; new checks require IntegerComparerCompare and prove
IntegerComparerCreate is not required. No whole-suite claim is made.

Review checked copied comparer arguments and signed ordering, version-update order,
no cache initialization for short lists, allocation/root cleanup and preserved
callback/ordinal paths. Profiles remain those of the validated CIL94/IR59/native79
snapshot; this managed-only slice adds no profile bump. All 94 upstream source hashes
and six frozen assembly hashes verify. Snapshot 91552fb; local main 48ecf14 remains
an ancestor. Integration preserves the other lane's separate Array.Clear edits.

Evidence: artifacts/alchemy-default-sort{,-refresh,-engine}. Engine discovery now
reaches an access failure in SetStorage<WaveGroupId>.Hash (IL_000e), reached through
EffectExecution.BeginWave and EffectChainCheckpoint.Restore. Source inspection shows
WaveGroupId is an internal readonly struct. The scan charges 1,391,917 metadata units
in 4,891ms; StateLookup still translates 15 methods, 11,616 units/426ms. Existing
diagnostic-only allowances are unchanged; no full-engine emission/execution occurs.
Core 289e07fb725526400180b50afe2d7c6973c674a1bb5618832c1ad70c3d274271.
Manifest 3ecec1c01326ae88c1e436c2cb6b1e8a090786f8f96a92ea99ac37f5bb98309d.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
