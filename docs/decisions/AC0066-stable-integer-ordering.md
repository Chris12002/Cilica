# ADR AC0066: Stable Int32-key ordering

Accepted 2026-10-07 for AC6.11.

Adapt pinned OrderBy/OrderByDescending and ThenBy/ThenByDescending into owned
managed iterators. The real IOrderedEnumerable<T>.CreateOrderedEnumerable<TKey>
contract uses the existing authenticated generic interface path, including custom
application implementations. Private sorter/cache interfaces allow admitted inline
elements without widening abstract-class signatures. Private upstream inheritance
is flattened; application rules and frozen inputs are unchanged.

Keys are evaluated primary-first, then each secondary key over the complete buffer.
Sorting uses original indices to preserve ties and the pinned comparison-based
introsort, including median pivot, insertion threshold, heap fallback, callback
order and descending sign tests. MapSort uses checked array slice coordinates
instead of Span. Comparer IndexOutOfRange faults become the exact bad-comparer
ArgumentException (upstream Comparison<Int32> name); other comparer faults retain
the original exception through the AC8.16 wrapper. Source/key-selector faults do
not enter that wrapper. No native sort or application-specific substitution.

Count, Any, Select and ToArray preserve cheap-count shortcuts, empty/single-item
behavior, selector/disposal order and first-element cache argument order. Enumeration
sorts even a singleton; ToArray returns its single-element buffer without keys.
Dispose releases sorted buffers/map/current while the query still owns its source,
selector and comparer. GetEnumerator retains clone/reuse behavior.

Seventeen differential probes pass 14,944 comparisons on macOS ARM64 with O0/O2,
ASan/UBSan and forced collection, including callback allocations. Coverage includes
all four ordering methods, three-level chains, repeated OrderBy, stable ties,
size thresholds, source/key/comparer faults, every sort-fault message code unit,
inline elements with references, custom ordering factories and terminal operations.
Natural 512/1024-element organ-pipe inputs execute exactly five/three heap fallbacks;
test-only entry counters observe that branch and complete callback hashes match .NET.
Two ownership checks pin 11 live objects before disposal, eight after disposal and
only the emergency exception after the query dies. Three key-profile negatives
remain diagnostic. All 19 slice and 69 selected adjacent query/generic-interface/
core/public-baseline checks pass (88 distinct checks). Logs are under
artifacts/alchemy-ordering/ac6k5-* and ac6k6-*.

Initial probes exposed existing direct wide/unsigned delegate restrictions; those
are now negative tests. A probe field initializer was moved into its constructor
to stay within the current initialized-receiver profile. Owned array-length and
out-parameter expressions use established supported IL forms. Compiler/runtime
profiles, grants and limits are unchanged. Review checked span-boundary equivalence,
index arithmetic, stable descending ties, terminal callbacks, exact exception text
and retained roots; no further changes were required.

Only Int32 keys are claimed. Default string comparison is culture-sensitive; the
reachable Comparer<String>.Default fallback still rejects even explicit ordinal
callers. Wide/narrow/unsigned delegate keys, other comparer defaults, partial sorting,
Order/Skip/Take and full replay remain unimplemented or unverified.

The frozen engine advances past the missing ordering declaration to a 256/256
metadata snapshot limit in FinalizeAccepted IL_032a (190 nominal declarations,
67 closed snapshots, 809 fields, 25,036 shared work units left). StateLookup remains
at 257/256 discovered identities. These partial counts are not emitted layouts.
Core SHA-256 ddb0ab1d2678883100366287e4fa109bcb740e28cee6856f81302c19cdcc9c31;
manifest bc8265ed653274b67352abd93cb6580d819dffa3fd516922b7342b7f2014803e.
The public baseline remains 31 native controls/16 gaps. Next separate delegate
classification from structural snapshots while preserving both resource bounds.
