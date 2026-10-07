# ADR AC0082: Default UTF-8 byte encoding

Accepted 2026-10-07 for AC7b.10.

Map Encoding.UTF8 and virtual GetBytes(String) to an owned managed implementation.
A per-runtime traced static owns the default encoder. Preserve the concrete
UTF8Encoding/UTF8EncodingSealed hierarchy; constructors and other members of
UTF8Encoding are not mapped. A public abstract Encode bridge uses the existing
bounded dispatch contract while the selected GetBytes declaration retains its
exact public virtual metadata. The default-only Encoding adaptation does not
implement generic buffer or mutable fallback machinery.

Two scalar passes over immutable UTF-16 replace pinned pointers, spans and SIMD.
The first counts with Integer64 and checks Int32 conversion overflow before one
byte-vector allocation. The second emits 1/2/3/4-byte UTF-8 sequences with identical
scalar advancement. Valid surrogate pairs combine; every isolated surrogate emits
U+FFFD. GetBytes never prepends a BOM; an input U+FEFF is encoded normally. Empty
input returns Array.Empty<Byte>; nonempty results are independent mutable arrays.
Null text raises ArgumentNullException for s; a null receiver faults first.
The array retains Cilica's existing UInt32 carrier and quotas. No native pointer,
new storage layout, intrinsic grant, profile or limit change is introduced here.

Ten probes pass 15,360 repeated comparisons against .NET, including exact byte
indexes, length/hash observations, boundary scalars, embedded NUL, malformed pairs,
long input, repeated allocations, independent buffers, identities and fault text.
Two native checks verify independent runtime-owned singletons, cleanup and recovery
after byte-array allocation quota failure. Decoder, section, other encoding and
configurable-constructor calls remain actionable diagnostics. O0/O2 builds retain
ASan/UBSan, forced collection, 2,048-byte frames and 30-second subprocess limits.
The cast-message reduction found and separately fixed AC8.22; the final tests use
its native v57 output. UTF8 tests pass in ac7b10g-tests.log. CharacterString and
core-library checks passed in ac7b10f-tests.log; no relevant changes followed.

The growing baseline's single optimized compilation exceeded 30 seconds once.
Split ordinary controls into six-probe compile batches and give the UTF-8 control
an exact one-static-root expectation. Its ownership check independently proves
that retained object is the encoder. The timeout and semantic assertions remain
unchanged. The final public baseline passes in ac7b10h-tests.log (13 checks, 50.178 seconds): 37 controls and
10 diagnostic gaps. Across focused final runs all 27 distinct selected checks pass.

Review checked surrogate consumption, output/count agreement, length arithmetic,
virtual signatures, concrete type names, immutable input, rooting across byte
allocation, empty singleton identity and unchanged denial boundaries. The three
upstream Encoding sources are pinned at the existing dotnet/runtime commit; all
62 upstream hashes verify. Other encodings, decoder/streaming APIs, mutable
fallbacks and direct UTF8Encoding construction remain unsupported.

Frozen application inputs remain unchanged. Engine UnitRoster.WithViews still
hits 256 snapshots and state lookup hits 257/256 identities. Full engine and
saved-game replay are unfinished. Evidence: artifacts/alchemy-utf8/ac7b10e-* through
ac7b10h-*, oracle.log and final/engine-and-state.json.
Core: a4c83e7a9600d388b892c3c80dde6f65f0d28ad896ebf716e461f9a263cf2eb1.
Manifest: 98ab1418119549d4a90d87c79bfe3ed6c9c1ba177f4e467811ab92b3e5c7b6cb.
