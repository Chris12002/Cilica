# ADR AC0052: Default Distinct and shared set storage

Accepted 2026-10-07 for bounded AC6.7.

ReleaseFinished needs Distinct<SpellId> following its projection. Adapt pinned
.NET 10.0.11 Distinct and its terminal optimizations with the existing default key
operations, value layouts and managed set algorithms. No game logic changes.

The iterator validates source eagerly, canonicalizes empty arrays, acquires its
source on first MoveNext and creates the seven-slot set after reading the first
item. It advances to the running state only after successful insertion. Later
moves skip duplicates; failures preserve Current and the same resume behavior.
Dispose clears Current, enumerator and set after disposing the source. Repeated
GetEnumerator calls preserve the existing first-thread reuse and clone contract.

Any uses an owned IQueryFirst<T> capability and bypasses hashing. Its raw fallback
uses IList Count/indexer or a reference enumerator with finally disposal. Where,
Select and nested Distinct retain the upstream first-element behavior and source
query state. Count and ToArray independently traverse the raw source, read generic
ICollection.Count before enumeration and build a temporary set. ToArray copies
stored entries, not original input values: a struct GetHashCode can mutate the
copy stored in the set. Final private capacity trimming is omitted; no callbacks
are skipped. Public layout/allocation identity of upstream private sets is not an
advertised contract.

Initial tests showed that allocating public HashSet inside the iterator also
made its unsupported IEnumerable factory a candidate in the conservative closure.
Extract the existing algorithm and fields into non-enumerable SetStorage<T>.
HashSet owns one storage object and delegates its implemented public members;
Distinct owns storage directly. There is still one hashing/equality implementation,
with existing by-value equality and mutable hash behavior. No reference-class
replacement for boxed struct enumerators or special interface-discovery exclusion
was introduced. Public HashSet enumeration remains diagnostic.

Eighteen authored probes pass 5,292 repeated native/.NET comparisons at O0/O2 with
ASan/UBSan, forced collection, exact cleanup and fixed frame bounds. Tests cover
resize/duplicates, nulls and string identity, mutated struct copies, callback
counts/failures, first and later hash-fault resumes, custom collection Count and
IList first-element effects, reference retention, iterator state, query chaining
and the reduced value Where/Select/Distinct/ToArray pipeline. Explicit comparer
and boxed List factory probes remain negative. The final composed run passes 59
tests, including existing Select, Where and all public compatibility controls.
All 11 existing HashSet tests pass after extraction, including reentrant removal,
reference release and exact intrinsic-grant denial. Failed/corrected logs and
frozen probe results are retained under artifacts/alchemy-distinct.

Review checked state assignments around faults, lazy source reads, disposal
precedence, mutable value copies, rooted storage ownership, wrapper initialization,
unchanged private grant authentication and unchanged compiler/resource profiles.
Pinned originals/adaptation notes are recorded in library/provenance.json.

The frozen engine advances to OfType<RoundStartedEvent> in ZombieSiege.AdvanceRound
IL_0009, with ICollection<DomainEvent> input; predicate Single follows at IL_0063.
State discovery remains at 257/256 identities. New owned core SHA256:
78e8688df832f27463853e31dedf6110f415edcbae165ea4604da5ef0c26a068.
Neither full engine execution nor saved-game replay has been validated.
