# ADR AC0173: Ordinal string hashing

Recorded 2026-10-08 for AC7b.19.

The frozen UnitId.GetHashCode reaches StringComparer.Ordinal.GetHashCode(string).
Implement that exact typed override as a managed null check followed by the existing
StringKeyOperations UTF-16 hash. Null throws ArgumentNullException with parameter
obj, matching the pinned comparer source. Direct and generic comparer calls use
ordinary virtual/interface dispatch. No intrinsic, native emitter or grant changes
are required. Parameterless comparer hashing, nongeneric object hashing, other
comparer factories and public String.GetHashCode remain diagnostic.

Numeric hashes are implementation-specific. Cilica deliberately reuses the owned
GetNonRandomizedHashCode adaptation shared by dictionary/tuple keys. It preserves
equal-string consistency and all UTF-16 code units, but does not promise numeric
equality with .NET's randomized public hashes. This matches the existing bounded
fixed-seed HashCode policy. Such hashes are not a portable fingerprint contract.

Six public-contract fixtures compare against .NET for repeated/equal strings,
embedded NUL and unpaired surrogate units, null faults, generic interface calls,
argument evaluation, null receiver ordering, and struct keys shaped like UnitId in
a resized dictionary. Two further fixtures compare numeric results to an independent
word-oriented model of the pinned owned algorithm: lengths 0 through 64 across six
starting units, plus all 225 pairs from 15 boundary UTF-16 units. There are 5,454
repeated native comparisons at O0/O2 with forced GC, ASan/UBSan, frame-budget and
cleanup checks. All 30 selected tests pass, including ordinal operations, pair
hashes, receiver discovery and the 46-control/one-gap public baseline.

Review checked the public null check before the private null-as-zero key helper,
UTF-16 padding/word ordering, unsigned wraparound, same-instance consistency,
subclass/interface dispatch, and exact retained singleton cleanup. Source provenance
records the deliberate hash policy. All compiler/runtime profiles, grants and
production/native/diagnostic bounds remain unchanged.

Evidence: artifacts/alchemy-ordinal-hash{,-refresh,-engine}. All six frozen input
hashes match. Frozen discovery advances to ReferenceKeyOperations<T>.Hash through
EffectSchedule.Compile, where an unrelated parameterless comparer hash is again
included: 1,182,935 charged metadata units in 4,044 ms. StateLookup remains 15
methods/11,529 units (445 ms). This new reachability diagnostic needs a reduced
probe before choosing a fix. Full engine/fingerprint/replay execution is unfinished.
