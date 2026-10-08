# Managed dictionary comparer construction

Recorded 2026-10-08 for compatibility batch 005.

Pinned DiffPlex Differ.cs selects StringComparer.OrdinalIgnoreCase or Ordinal from
its ignoreCase Boolean. BuildPieceHashes uses Dictionary<string,int> hash/equality,
Count, TryGetValue and indexer assignment; it does not call Compare or a culture
factory. Re-reading the pinned blob confirmed the batch-001 source identity.

Replace both owned InternalCall comparer constructors with managed bodies. Store
the supplied comparer in an ordinary traced field and forward hash/equality through
the existing generic interface dispatch pipeline. This admits aliases, custom
comparers, null defaults and runtime selection without a syntactic getter proof.
Remove only the two owned Dictionary CreateOrdinal factory grants. The legacy
compiler recognizer remains for its old declaration shape; managed constructors
bypass it. HashSet/List bridges, compiler/native profiles and limits are unchanged.

Null keys fail before callbacks. Negative capacity fails before storage allocation;
argument evaluation remains normal IL order. Hashing/equality can allocate or throw
and retain their rooted key/comparer/value operands. Existing collision order,
free slots, duplicate behavior, overwrite versions and value roots are retained.
Default Find hashes a copy; default Insert/Remove hash their own key parameter.
Custom comparer calls receive value copies.

The pinned .NET Dictionary constructor also normalizes an explicitly supplied
EqualityComparer<T>.Default for value keys. Simply forwarding it loses mutations
of a value key's hash receiver: an independent regression exposed this difference.
The owned cache marks only its default instance using existing exact IsValueType
selection, a private Boolean and assembly-visible managed accessors. Dictionary
uses that marker to retain its default path. Custom EqualityComparer subclasses
remain custom, and reference-key default comparers retain their supplied identity.
No reflection, new compiler special case, eager default creation or public marker
API is added. The marker adds no root or separately allocated object.

Ten independent roots compare against normal .NET at O0/O2 with ASan/UBSan,
collection before allocations, exact permanent-root counts and cleanup assertions.
They cover runtime selection, collisions, mutable value copies/default receivers,
callback order and faults, constructor evaluation, null validation and identity.
Historical rejection cases now run as native controls. A stale typed ordinal hash
rejection is replaced with equal-text hash/null-fault controls; numeric equality
with randomized .NET hashes is deliberately not asserted.

Only already admitted key/default/dispatch shapes are covered. Public Comparer,
copy construction and unimplemented collection APIs are not introduced. Reentrant
mutation of the same dictionary from a callback has not been newly verified.
OrdinalIgnoreCase and CurrentCulture retain exact missing-member diagnostics.
Invariant lowercasing from the concurrent thread is not a substitute for Unicode
ordinal-ignore-case equality/hash. Full DiffPlex remains unexecuted and may expose
further integration defects after that remaining gate closes. See the
[batch results](../compatibility-candidates/batch-005-results.md).
