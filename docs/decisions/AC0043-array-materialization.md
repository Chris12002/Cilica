# ADR AC0043: Array materialization and query cursor preservation

Accepted 2026-10-07 for bounded AC6.4.

Implement Enumerable.ToArray with selected collection Count/CopyTo and generic
reference-enumerator fallback. Preserve null-source faults, Count exactly once,
zero-count copy suppression, exact result lengths and canonical Array.Empty<T>.
An owned IQueryMaterialization<T> contract selects Where's source traversal without
changing the query's iterator state or Current. It avoids introducing Func<T,bool>
into unrelated inline-value array closures. Array predicates see each current slot;
fallback using/finally retains Current, predicate and Dispose fault precedence.

Use a managed MaterializationBuffer<T> with checked count, bounded growth arithmetic
and traced typed arrays. It replaces upstream private stack/pool scratch storage;
allocation strategy and private layout are not represented as identical to .NET.
It deliberately implements no enumeration contracts: an internal List accumulator
would introduce the still-unsupported boxed List enumerator into interface closure.
Growth allocates and copies before publishing storage; successful result arrays
retain element identity and nested reference roots. Empty results use Array.Empty.
No native profiles, compiler operations or grants change.

Twelve authored probes pass 3,528 repeated native/.NET comparisons at O0/O2 on
macOS ARM64, ASan/UBSan, forced collection, strict cleanup and unchanged native frame
quotas. Coverage includes collection faults, inline and empty values, reference
covariance, three buffer expansions, active query cursors, first-enumerator identity,
references retained across allocations and exception/disposal precedence. 29
adjacent query/cardinality/predicate and negative-boundary checks pass. An initial
reference probe indexed a correctly filtered empty result; corrected inputs and
all logs are retained in artifacts/alchemy-to-array. A test cancellation-token
analyzer error was fixed before running adjacent tests. Review checked copy/fault
order, result identity, callback counts, ownership and closed query isolation.

The exact pinned ToCollection.cs is vendored with SHA-256 provenance. Where's
existing pinned speed-optimization source supplies the cursor-preserving path.
Frozen engine discovery advances to unmapped HashSet<T> from PrepareForfeit;
StateLookup retains its 257/256 identity limit. This does not establish engine
execution or saved-game equivalence. General boxed collection interface factories,
including a List in the conservative enumerable closure, remain diagnostic; direct
List.ToArray is separately supported. Unsigned array aliases and value-payload
predicates remain outside this slice. Full replay remains unfinished.
