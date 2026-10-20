# DiffPlex: profiling the remaining execution gap

2026-10-20; Apple M4 Pro, macOS ARM64, Apple Clang 17, .NET 10.0.11.
Committed compiler/library baseline: `8b552e4`, native v95.

## Finding

Fresh emission reproduces **165.410 us/diff versus 5.229 us for non-tiered .NET:
31.63x**. The remaining cost is spread across generic borrow handling, redundant
runtime type validation, and framed library calls. There is no demonstrated
single change that closes the gap. The strongest combined diagnostic experiment
reaches **115.110 us**, still approximately **22x** the measured .NET reference.
That experiment is not a production compiler change.

The next substantial compiler target should be **typed internal calls and local
storage**, starting with non-allocating scalar helpers and proven synchronous
borrows in the dictionary/hash path. Simple methods should become simple native
operations while roots remain published where allocation can occur. Merely
inlining runtime helpers or adding another borrow cache did not produce a large
win. Broad descriptor membership is also a concrete, independently measured
cost worth fixing as a coherent runtime change.

## Current checkout and measurement identity

The working tree's `ObjectCppEmitter.cs` contains an uncommitted rollback to the
older emitter. It was left untouched. Measurements use an isolated `git archive
8b552e4` snapshot, freshly built and freshly emitted, under
`artifacts/remaining-gap-20261020/source`. The resulting C++ is byte-identical to
the previously accepted v95 candidate. This report describes the committed v95
baseline, not the dirty working tree. The cause of the local rollback is unknown.

- DiffPlex input DLL SHA-256:
  `652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`.
- Input PDB SHA-256:
  `4eac2d8ae6c2e7efaade8bca707bcbbe5673dc317cf141176fd4ce7b6ee285b8`.
- Fresh owned core SHA-256:
  `655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1`.
- Fresh baseline C++ SHA-256:
  `99464f8a098b918b119aa947b0f794496487a894eae45fa4e63cbe8193ea4a98`.

Native flags: C++20, O2, NDEBUG, no native exceptions/RTTI, no sanitizers.
Each variant first matches all **523 output values**, then runs three independent
processes, each with at least 300 ms warmup and seven timed samples of 256 diffs.
Orders rotate/reverse. Every timed checksum is 6146 per diff. Builds and sampling
finish separately from timing; no outliers are discarded. .NET uses 16,384
diffs/sample. Timing results are host-inclusive and specific to this workload.

Non-tiered .NET disables both tiering and ReadyToRun, matching the prior 31x
comparison. Default .NET measures 2.645 us median, but retains slow initial
samples up to 19.734 us while tiering settles; this is not a steady-Tier1 claim.
Unity timing was not rerun. Retained IL2CPP output is structural reference evidence.

## Where the time goes

Two independent three-second, one-millisecond statistical samples produced
**4,557 application-exclusive samples out of 5,106 total**. Counts below are
exclusive, so the named groups do not double-count their callees. Sampling is
approximate; removing a sampled cost does not guarantee an equal wall-time gain.

| Group | Application samples | Share of application samples |
| --- | ---: | ---: |
| Frame-slot resolution, location dispatch, borrow registration, root-chain validation and borrow lookup | 1,042 | 22.9% |
| Broad System.Object descriptor predicate alone | 737 | 16.2% |
| StringKeyOperations helpers, Dictionary.Hash/HashKey and ordinal GetHashCode | 765 | 16.8% |

The last group includes generated frame work inside those methods; it is not
16.8% pure hash arithmetic. The broad object predicate checks a 44-descriptor set.
Its largest sampled caller is `location_reference` (361 samples), followed by
`mark_graph` (110), `array_location_storage` (108) and `store_reference_element`
(94). This connects type validation directly to borrowed reference loads and
array accesses. Some calls are GC checks and must remain checked.

System-library code accounts for 10.8% of total samples, mostly allocation/free
and clearing routines. Collection is visible, but the profile does not establish
GC as the main remaining bottleneck. Replacing the collector is not the next
supported conclusion.

A separately instrumented **single cold diff, including initialization**, records:

- 4,277 execution-frame reservations and 2,703,800 cumulative reserved bytes.
  This is reused arena space, not 2.7 MB of simultaneous live memory or managed allocation.
- 27,931 descriptor predicate calls and 107,850 comparisons in their linear loops.
- 6,892 root-chain visits; only 93 fallback borrow-list visits after the cache change.
- 283 calls each to StringKeyOperations.Hash, Pair and Rotate; 256 dictionary
  TryGetValue and Find calls; 27 array allocations and 264 string allocations.

The nine-byte IL Rotate method still enters Storage/RootScope, checks pending
fault/depth/budget state, and computes through frame slots. Direct control flow
removed the dispatcher; it did not remove this calling convention.

## Controlled experiments

These probe causes, not accepted production capabilities. All match the complete
DiffPlex transcript. Costs interact; speedups must not be multiplied or added.

| Experiment | Median us/diff | Speedup versus same-series baseline |
| --- | ---: | ---: |
| Baseline | 166.341 | 1.000x |
| Permit inlining only in borrow/location helpers | 165.715 | 1.004x |
| Hashed descriptor membership for sets of at least eight | 146.017 | 1.139x |
| Permit inlining throughout runtime helpers | 160.923 | 1.034x |
| Borrow helper inlining + descriptor membership | 144.290 | 1.153x |
| Runtime helper inlining + descriptor membership | 137.382 | 1.211x |
| Load the borrowed string once before hashing | 151.537 | 1.098x |
| Above, plus put Pair/Rotate arithmetic inside Hash | 147.351 | 1.129x |
| Runtime helper inlining + descriptor membership + flattened hash | 126.386 | 1.316x |

The hash variants freshly compile the owned C# core and regenerate C++; they do
not replace the hash algorithm or change the input library. Snapshotting avoids
repeated generic borrowed-reference resolution. Flattening adds a further 2.8%
reduction relative to the snapshot variant. Hash collision behavior and arbitrary
UTF-16 inputs still require targeted conformance before adopting either variant.

A subsequent series tests typed frame storage on the native stack while retaining
arena budget accounting, root publication and borrow lifetimes:

| Frame experiment | Median us/diff | Speedup versus same-series baseline |
| --- | ---: | ---: |
| Baseline | 171.963 | 1.000x |
| Previous combined variant | 127.813 | 1.345x |
| Native stack storage for all generated frames | 150.999 | 1.139x |
| Combined variant + native stack storage | 115.110 | 1.494x |

Native storage saves 9.9% relative to the combined variant in this series. Its
largest reported O2 native frame is **2,608 bytes**, versus 400 for the earlier
variants, exceeding the existing 2 KiB gate. This is evidence to investigate
selective scalar/local lowering, not permission to move every frame to the stack.
A separate O1 ASan/UBSan run with direct flow and collection before every allocation
matches all 523 values for the combined native-frame experiment without diagnostics.
It does not establish bounded-stack recursion, resource-limit or general semantics acceptance.

Negative evidence is retained too: diagnostic removal of registration validation,
caching resolved frame addresses in a larger Location carrier, and their combination
did not show a convincing benefit. That series was noisy (baseline 177.243 us,
range 165.441–534.628); the combined borrow ablation was 177.164 us. These probes
also weaken stale/malformed-carrier detection, so they cannot be adopted as-is.
They do not validate or refute a genuinely smaller typed-pointer calling convention.

## Roslyn, .NET and IL2CPP comparison

Roslyn produces IL; CoreCLR's JIT produces .NET native code. The exact input DLL
was built with Roslyn
`5.9.0-1.26379.115+14fbf8d5271c98133561eb55185fdb05b286f578`, Release, C#14,
netstandard2.0. That exact source revision remains unavailable. The inspected
Roslyn source checkout `e4e2833be7abbc54776788ca35d5c4d2697b0261` is comparative
only; exact emitted IL and hashes are authoritative. The captured owned core uses
Release C#9, optimization enabled, no PDB; its build options are retained.

| Case | Exact IL / Roslyn and .NET evidence | Retained Unity IL2CPP evidence | Implication for Cilica |
| --- | --- | --- | --- |
| Local output borrow | DiffPlex BuildPieceHashes, token 06000011: IL_007d `ldloca.s 3`, IL_007f TryGetValue. Roslyn EmitAddress.cs:288–315 emits a local address. Retained .NET 10.0.11 non-tiered TryGetValue is 84 native bytes and stores directly through its supplied address. | DiffPlex.Benchmark.cpp uses `(&V_3)`. Generics__3.cpp:6967 accepts `int32_t*` and writes through it. | Native typed borrows are a plausible structural target; another lookup cache is not the same change. Preserve owner roots and synchronous lifetime proof. |
| Type checks around accesses | Ordinary field access is IL `ldfld`; .NET FindValue uses direct field/array loads and bounds checks, without scanning the whole admitted type set. | Generics__3.cpp:5608 FindEntry reads typed fields and inline entry-array elements. Sealed casts use class-pointer equality; class casts use hierarchy-depth/index checks in ClassInlines.h:16. Interface dispatch can still scan interface offsets. | Avoid repeatedly re-proving known internal receiver types; use efficient checked membership where the runtime still needs it. Keep public handles and GC validation distinct. |
| Hash helper chain | REA on the exact fresh core: Hash 0600024e is 108 IL bytes, five `ldind.ref` sites and six Pair/Rotate call sites. Rotate 0600024c is nine bytes. Calls at Hash IL_0051/005b reach Rotate/Pair for the one-character tail. .NET's pinned GetNonRandomizedHashCode uses local accumulators and direct word reads in one routine. | mscorlib.cpp:23059 uses local accumulators and a direct UTF-16 pointer loop for its legacy hash. Its algorithm differs from the current .NET/Cilica hash; the comparison is about execution shape. | Load stable input once and remove full managed-frame overhead from tiny helpers. Do not copy Unity's hash semantics. |
| Dictionary entry representation | .NET v10.0.11 Dictionary.Entry is a struct in an array; actual FindValue native code indexes 24-byte entries. The constructor recognizes ordinal string comparers. The measured non-tiered native code still makes indirect comparer calls. | FindEntry and TryGetValue address inline Entry-array fields with GetAddressAt. | Cilica uses separately allocated DictionaryEntry classes and another pointer load. This is a real structural difference, but its independent speed impact was not measured here. |

Matching .NET library sources are pinned at v10.0.11,
`79d0c463f1b55624c874a11585f7e47731e8d675`. Source hashes were rechecked.
The retained Unity output uses the same input DLL; stripped assembly tokens differ,
so comparisons follow method identity rather than assuming token equality.
The supported JIT dump from the prior investigation is reused for native inspection;
.NET execution timings above are fresh. REA supplied current core metadata/IL
anchors; no binary reverse engineering was needed.

## Recommended next work and review

1. Design a typed internal fast path for scalar arguments/results and proven
   borrows, with scalar temporaries available to native register allocation.
   Start with no-allocation/no-callback helpers, retain framed cold/fault paths,
   and measure the entire Dictionary/hash chain. Heap owners must stay rooted
   across allocation; aliases, stale carriers, narrow/wide values and reference
   stores need explicit treatment. This targets the largest structural mismatch;
   the eventual gain is not established by these experiments.
2. Replace broad linear descriptor membership with a checked efficient representation,
   or eliminate redundant checks only when exact compiler proofs justify it.
   The address-hash probe preserves membership checks and demonstrates about 12%
   time reduction by itself; retain checked GC/public-boundary behavior.
3. Use the hash snapshot as a small, measurable library improvement within that
   work. Investigate inline dictionary entries and allocation layout afterward;
   do not present their potential as a measured gain.

Review checked revision identity, transcript equality, sampling denominators,
non-additive effects, native frame sizes and the distinction between diagnostics
and production semantics. The repeated emitter rollback must be reconciled before
any subsequent implementation; this investigation does not overwrite it. No
production source, profiles, grants or hosted workflows changed.

Evidence and replay instructions: [remaining-gap-20261020/README.md](remaining-gap-20261020/README.md).
Earlier implemented slices: [execution-slices-20261020.md](execution-slices-20261020.md).
