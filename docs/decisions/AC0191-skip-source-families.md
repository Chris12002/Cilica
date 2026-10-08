# ADR AC0191: Skip source families

Recorded 2026-10-08 for AC6.21.

The frozen shield-choice path uses wave.Acquisitions.Skip(1).ToArray(). Implement
Enumerable.Skip for ordinary enumerable and IList sources using the pinned Skip,
Skip.SpeedOpt and SkipTake.SpeedOpt algorithms. Keep the existing owned iterator
composition model instead of introducing runtime-owned query primitives.

Null is validated immediately. Empty arrays return the shared empty array.
Nonpositive counts are clamped, with source identity retained only for recognized
optimized query iterators. Ordinary enumerables defer acquisition until MoveNext,
do not read Current for skipped elements, retain state on user faults, and dispose
at completion. Repeated skips merge offsets unless signed overflow requires a new
outer iterator. List views retain index-based mutation behavior, IList/IReadOnlyList
identity, read-only mutation failures, indexed access, count, contains/index search
and copy semantics. Overflowed list offsets return the empty array.

The list-view copy path preserves destination span validation, covariance rejection,
and source span bounds even for an empty/null destination. Only exact arrays and
List objects use captured storage; covariant arrays/custom lists use their indexers.
ToList follows the pinned capacity rules. Index search uses copied values, matching
the unconstrained MemoryExtensions span overload and EqualityComparer.Equals;
mutable value receivers cannot mutate stored source elements. The investigated
range-search intrinsic was unnecessary and removed; existing prefix search is
unchanged. Value-return storage uses the existing SetCurrent materialization pattern.

IQuerySkip marks all eight currently owned optimized query families. Their positive
Skip specializations deliberately call the existing unsupported-member sentinel
until callback/order-specific implementations are added. This produces compile
errors instead of substituting generic iteration. Admission is conservative: a
closure containing possible unsupported targets can be rejected even if one call
would dynamically use an ordinary source. This slice does not claim general LINQ
Skip support, optimized query composition, Take or SkipWhile.

All 43 new checks pass: 17 probes/7344 repeated O0/O2 comparisons, one translation
check and eight optimized-source rejection checks. Coverage includes min/max counts,
repeated enumeration, lifetime/identity, callback/fault traces, current/disposal,
list mutations, capacity, generic/nongeneric list views, copied/allocating equality,
reference-bearing values, nulls, covariant arrays and exact exception text. ASan,
UBSan, forced collection, frame exhaustion and exact cleanup are enabled. Empty
iterator caches are explicitly warmed and counted in affected fixtures. The large
multi-terminal fixture was separated into a small reader to stay within the
unchanged native stack bound; no implementation limit was increased. 81 adjacent
iterator/search checks also pass (124 total). All 84 pinned source hashes verify.
No full-suite or additional-platform claim.

Evidence is in artifacts/alchemy-skip-sources, alchemy-skip-sources-refresh-v2 and alchemy-skip-sources-engine. Testing uses an
isolated source snapshot of committed 220e157 plus this slice's exact owned files;
concurrent UInt32 compiler work is excluded. The tested compiler is CIL83/IR54,
layout27/native76. No compiler source/profile/grant changes belong to this slice.
Review checked mutable equality, source-list and destination-array validation,
clone/dispose/fault transitions, checked/unchecked arithmetic and ordinary managed
rooting. Main 48ecf14 remains an ancestor. The unmodified frozen inputs remain data.

Frozen engine diagnostic result:
CIL2003: 2048/2048 cached snapshots, 807 nominal declarations, 1321 closed generic
snapshots, 4312/6144 fields. Explicit roots: 184 values, 1453 references, 161 vectors.
These are discovery counts, not emitted layouts.
Location: Alchemy.Engine.Effects.EffectProgramRunner::Run token 0x06000c21 specialization 54826c169a77c203417714436ea89e447140a1afc0174535c1ef4d7888a0e758 IL_0000
Metadata units: 1088915; milliseconds: 4602.
StateLookup: 15 methods, 11613 units/467 ms.
Core SHA256: ed7a87db7e956d1bdb45089b93e4a027148521910ceb55f2ddba1e3fb97b3ede.
Manifest: 7c6ad5a2ef3bc8224a8eb19f1b55d998a2bdb0096516d461d0377116b73d9dae.
The README build command also now includes the three already-committed ordinal-
ignore-case sources; their omission caused a documented-core build failure.
No engine code is emitted or executed. Full engine, unchanged fingerprint and saved-
game replay remain unfinished.
