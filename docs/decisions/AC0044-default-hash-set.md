# ADR AC0044: Default HashSet storage and reference-aware removal

Accepted 2026-10-07 for bounded AC3.9.

Add selected HashSet<T>/ISet<T> metadata and default construction, Add, Contains,
Remove, Clear, Count and TryGetValue for existing admitted integer, string and
inline IEquatable key operations. Generic collection/set mutation dispatch uses
those same bodies. Preserve prime capacities, one-based buckets, entry order,
free-slot reuse, stored values, copied equality receivers and mutation of the
hashing receiver. HashSet accepts null with hash zero. Empty lookup/removal skips
hash callbacks; Add allocates its initial arrays before hashing as pinned.

Use traced inline HashSetEntry<T> arrays rather than reference entry wrappers.
Capture the entry array before hashing and the bucket array/index before equality,
preserving the upstream managed byref across reentrant callbacks and resizes.
Reuse the existing pinned prime helper. New storage is allocated/copied before
publication. Version increments only on successful insertion; clear/removal retain
its pinned behavior. Custom comparers, randomized string collision hardening,
capacity/enumerable constructors, enumeration, copying, set algebra and serialization
remain unsupported. The string hash uses Cilica's existing bounded key profile.

Review caught a removal detail: only reference-bearing values are cleared in freed
slots. A reentrant Equals can remove its own entry and still let outer TryGetValue
read the old value. An unconditional-clear mutant returns 7 instead of .NET's 8
for input 1/0, at both optimization levels. Add the exact private zero-argument
KeyOperations<T>.ContainsReferences grant, authenticate its declaration/caller and
fold it from the validated expanded layout. Scalars/empty values return false;
references and nested reference-bearing values return true. No new runtime or IR
operation is introduced; integer-object CIL v8 records this selection. Native v45,
object IR v18, exception IR v5 and layout v17 remain unchanged.

Eight ordinary probes, the reentrant regression and advanced public control pass
2,940 repeated native/.NET comparisons at O0/O2 on macOS ARM64 with ASan/UBSan,
forced collection, strict cleanup and unchanged frame limits. Two native lifetime
checks release one nested key reference on Remove and the other on Clear while
the set remains rooted. Four grant removals, an external private helper call and
unsupported comparer/enumeration routes fail compilation. Seven contract/boundary/
GC checks and 42 adjacent dictionary, constructor and public baseline checks pass.
The regression's first static fields lived in an unsupported value owner; moving
its state to an admitted static holder enabled the focused before/after comparison.
All logs, source hashes and closure results are in artifacts/alchemy-hash-set.

Frozen engine discovery advances from unmapped HashSet<T> to missing
String.Concat(string[]) in BoardCatalogue.RequireBoard IL_0036. StateLookup still
hits 257/256 discovered identities. These are conservative discovery observations;
later argument proofs, complete engine execution and saved-game replay remain
unverified. The public baseline now has 31 native controls and 16 known gaps.
