# ADR AC0064: Default integer comparers

Accepted 2026-10-07 for AC5a.2.

OrderBy's default integer keys need Comparer<T>.Default. Adapt the pinned comparer
base and generic implementation for Int32, UInt32, Int64 and UInt64. Two separate
core-only capabilities authenticate exact Create and Compare declarations on
ComparerOperations<T>. Create selects an ordinary managed factory; Compare selects
the existing typed primitive CompareTo body with a borrowed first argument. No
native comparison algorithm, application method substitution or reflection fallback
is introduced. Primitive signedness and the original evaluation order remain intact.

Each closed ComparerCache<T> owns a private readonly singleton through the existing
runtime-instance initialization machinery. A same-owner accessor reads the private
field; cross-owner generic static-field visibility is not widened. GenericComparer<T>
retains its exact IComparable<T> constraint. The base supports existing abstract and
typed interface dispatch, including application subclasses. Nongeneric comparison,
comparer-object equality/hash, delegate factories and other defaults remain diagnostic.
The factory rejects unsupported type arguments even when Compare is never called.

Eight probes pass 2,352 native/.NET comparisons at O0/O2 on macOS ARM64 with forced
GC and ASan/UBSan. Signed/unsigned 32/64-bit ordering, typed calls, user overrides,
null receivers and singleton identity are covered. Two native ownership tests prove
one distinct comparer per selected closed type per runtime instance, no cross-instance
ownership, and reclamation after roots are cleared. Eight negative cases, exact
independent grant removal and a foreign private-helper caller are checked. Nullable
and Guid negatives were made factory-only to test the intended gate rather than an
earlier unsupported abstract inline signature. The foreign caller is rejected by
the existing lexical gate before helper selection.

The original combined ownership harness exceeded the existing 2048-byte frame limit
under O0 sanitizers. Heap-owning its instances alone did not fix instrumentation
spills; splitting its collection and ownership assertions into small functions did.
No generated-runtime limit or warning was disabled. Eighteen comparer checks and
41 adjacent ordinal/integer/core/public-baseline checks pass (59 distinct checks).
Logs: artifacts/alchemy-integer-comparer/ac7b7-5-*, ac7b7-6-*; the prototype log
prefix predates placement of this slice in the numeric AC5a workstream.

Review checked exact helper shape/grants, closed factory constraints, scalar borrow
calling convention, unsigned ordering and independent singleton roots. Existing
compiler/native/layout profile versions stay unchanged. The source pin and adaptation
are recorded in library/provenance.json. Core SHA-256:
1b78419273a42240a2f66fcd9b7eea88f0d667eec9d0a66cb7c682d433d68762;
manifest ee0913ef3e8469833f73a32df254c1f4cbac28de3beac01ccd45ca26ff5ba02e.
The public baseline remains 31 native controls/16 gaps. Frozen engine still needs
IOrderedEnumerable/OrderBy; state remains at 257/256 discovered identities.

Ordering implementation must preserve the pinned stable index-map/introsort callback
order and exception wrapping. InvalidOperationException(String, Exception) is not
yet implemented and is needed for sort comparer faults. Default string comparison
is culture-sensitive and cannot be replaced with ordinal comparison. A reachable
Comparer<TKey>.Default fallback in a generic ordering body will currently reject
string keys even when callers normally supply StringComparer.Ordinal. That branch
needs correct default semantics or proven nonnull-call specialization before claiming
the full Alchemy ordering chain. Complete engine emission/replay remains unverified.
