# ADR 0141: Dictionary storage and closed key trial

Status: Accepted for the direct-core trial; System.Dictionary mapping pending

## Decision

Adapt the pinned Dictionary FindValue, TryInsert and Resize algorithms and
HashHelpers prime selection into Cilica.Library.TrialDictionary. Preserve uint
hashes, one-based buckets, collision chains, insertion order, duplicate behavior
and overwrite. Replace unsafe interior references and inline entry arrays with
checked reference-entry vectors until value vectors are implemented. No entry
object is returned to callers. Empty dictionaries allocate backing arrays lazily.
Retain unchanged upstream files, license and adaptation hashes in provenance.json.

Three private, exactly granted KeyOperations<T> helpers select closed integer
equality, ordinary integer hash bodies or exact inline IEquatable<T>/Object hash
overrides. They do not execute input assemblies or use host reflection. Resolve
one bounded closed declaration, then authenticate constrained value targets with
the existing metadata resolver. Reference/string keys, default value hashing,
explicit-interface fallbacks and unsupported signatures remain diagnostics.

Preserve the source's distinct mutable-key semantics: FindValue hashes a copy;
TryInsert hashes its own key parameter byref; equality receives copies. The
runtime-coupled helper does not implement a public EqualityComparer.Default
singleton or grant general application access to runtime internals.

The trial supplies a constructor, Count, TryAdd, TryGetValue and indexer writes.
It intentionally has its own Cilica.Library name. System.Dictionary methods
also implement collection interfaces; their exact flags and array relationships
must be supplied before a public reference mapping is admitted. Enumeration,
removal, read indexers, comparer/capacity constructors and other APIs remain absent.
The owned Array declaration only allows Roslyn to emit existing ldlen operations;
it adds no Array methods or interface compatibility claim.

## Evidence and review

Five independent probes run as ordinary .NET Dictionary consumers. A test-only
source adapter changes only the dictionary type identifier for the owned core
build. The resulting probes supply 1,470 repeated native O0/O2 sanitizer
comparisons covering growth, collisions, 32/64-bit and Guid keys, struct/reference
values, independent copies, missing defaults, mutable equality/hash receivers,
callback exceptions, collection before allocation and frame/ownership cleanup.

Tests remove each exact helper grant and reject external helper callers and
unsupported reference keys. Closed declaration tests cover substitution, ambiguous
and missing members, invalid owner arity and metadata budgets. The full solution
build passes; 63 checks passed in the composed run, and the corrected declaration
fixture plus both native cases pass in the final three-check run (64 distinct).

Review caught and corrected the FindValue/TryInsert receiver-copy difference and
preserved upstream prime growth/overflow behavior. A separate compiler correction
handles residual stack values at throw (ADR 0140). No remaining issue was found
in this trial. The actual core build records 207 inputs and 71 requested grants;
its digest is evidence, not production approval. Local main remains an ancestor.
The public kernel still stops at Dictionary; array/interface integration and the
full native transcript remain required before milestone 6 can complete.
