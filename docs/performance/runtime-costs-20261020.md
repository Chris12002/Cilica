# DiffPlex runtime cost investigation

Recorded 2026-10-20, Apple M4 Pro, macOS ARM64, Apple Clang 17, .NET 10.0.11.
Compiler baseline: `b44dbc6b5e1cc4e2244137a6641fb0cd68f1ac86`, native v92.

## Finding

The roughly 70x gap is reproducible against the previous .NET configuration.
It is substantially caused by generated execution machinery and avoidable owned
library work, rather than complicated IL emitted by Roslyn. The experiments below
recover **2.70x** on DiffPlex, but leave approximately **25.5x** against the freshly
measured non-tiered .NET reference. They do not account for the entire gap and
are not production compiler changes or general conformance evidence.

The largest demonstrated direction is **direct native control flow**, followed
by eliminating temporary arrays in character-to-string conversion. Merely
allowing Clang to inline the step dispatcher leaves much of its cost intact.

## Controlled measurements

All native timings use Clang C++20 O2, NDEBUG, no native exceptions/RTTI, no
sanitizers. Compile and diagnostic runs finish before timing. Each variant has
three independent processes, at least 300 ms warmup per process, seven samples
per process, 256 diffs per native sample. Orders rotate/reverse; all samples,
including outliers, are retained. Checksums are 6146 per diff. Each native variant
also matches the complete 523-value .NET transcript before timing.

The eight-way factorial experiment separates the three requested costs:

| Variant | Median microseconds/diff | Speedup vs baseline |
| --- | ---: | ---: |
| Baseline | 369.402 | 1.00x |
| D: inline steps through a switch dispatcher | 321.300 | 1.15x |
| B: indexed borrow lookup | 346.283 | 1.07x |
| T: hashed descriptor membership | 338.386 | 1.09x |
| D+B | 301.290 | 1.23x |
| D+T | 287.439 | 1.29x |
| B+T | 305.596 | 1.21x |
| D+B+T | 269.924 | 1.37x |

These are interacting costs, not additive percentages. The combined profile
still spends substantial samples in the dispatcher and its per-step pending
exception checks. A distinct experiment replaces constant step transitions with
native branches while retaining the existing method entry, arena/root scopes,
and exception-dispatch checks:

| Direct-control-flow experiment | Median microseconds/diff | Speedup |
| --- | ---: | ---: |
| Paired baseline | 364.615 | 1.00x |
| D+B+T | 268.040 | 1.36x |
| Direct branches+B+T | 184.631 | **1.97x** |

All 144 methods in this closure have zero local exception regions. The prototype
rewrites 10,662 statically known transition edges. It preserves pending-fault
routing, but this workload cannot establish correctness for catch/filter/finally
control flow. This is a limitation of the experiment, not a restriction proposed
for Cilica's supported semantics.

A final, independently paired experiment replaces the owned static
`Char.ToString(char)` body with direct use of the existing string allocator.
The original framed entry remains; the temporary char and Int32 arrays disappear.

| String construction experiment | Median microseconds/diff | Speedup |
| --- | ---: | ---: |
| Paired baseline | 365.646 | 1.00x |
| Direct branches+B+T | 185.656 | 1.97x |
| Above plus direct character string | 135.182 | **2.70x** |

This reduces the preceding prototype's time by 27.2%. It changes allocation
behavior, so allocation-failure origins and resource exhaustion need an explicit
production design; output equivalence alone does not settle that contract.

Raw ranges, sample counts and medians are retained in
[factorial-summary.json](runtime-costs-20261020/factorial-summary.json),
[cfg-summary.json](runtime-costs-20261020/cfg-summary.json) and
[char-summary.json](runtime-costs-20261020/char-summary.json).
No timing run was repeated to select a favorable result.

## .NET comparator and the meaning of 70x

The same retained input DLL and host were rerun sequentially with the factorial
experiment. Their 523-value transcript also matches. At 16,384 diffs/sample:

- Tiering and ReadyToRun disabled, matching earlier comparisons: **5.303 us**
  median, 5.166–5.430 us range; baseline ratio **69.66x**.
- Default runtime settings: **2.618 us** median, 2.536–19.862 us range. Every
  process has a slow first measured sample while tiering settles; all are retained.
  The median ratio is about **141x**, but this is not an isolated steady-Tier1
  experiment. Do not use 70x as the gap to .NET's best normal execution.

The latter needs a longer, demonstrated stable warmup for a competitive claim.
The host's old comment says tiering is disabled; the actual environment is varied
by the supervisor and recorded by the experiment. NativeAOT is not involved.
Unity timing was not rerun; its retained generated code is used for structural
comparison, not a new speed comparison.

## What Roslyn, .NET and Unity actually do

Roslyn generates IL; CoreCLR's JIT generates .NET machine code. Their roles must
not be conflated. Exact DLL/PDB hashes and PDB compiler identity are retained in
[the preceding Roslyn investigation](internal-references-20261019.md#follow-up-roslyn-il-and-source-inspection).
Input compiler: `5.9.0-1.26379.115+14fbf8d5271c98133561eb55185fdb05b286f578`,
Release, C#14, netstandard2.0. That exact Roslyn source commit was unavailable;
local source revision `e4e2833be7abbc54776788ca35d5c4d2697b0261` is comparative
source only. The exact emitted IL remains the authoritative input evidence.

| Case | Roslyn IL / .NET native evidence | Unity IL2CPP evidence | Cilica cost |
| --- | --- | --- | --- |
| Control flow | `BuildPieceHashes` is 186 IL bytes/75 instructions; JIT output uses ordinary branches and calls | `DiffPlex.Benchmark.cpp` emits labels/branches in the method | 114 steps for this method; general dispatch and frame machinery persists through ordinary calls |
| Output borrow | `BuildPieceHashes` IL_007d `ldloca.s 3`, IL_007f `TryGetValue`; .NET's 84-byte native TryGetValue writes through its supplied address | Same call uses `(&V_3)` and `int32_t*`; generic TryGetValue writes through it directly | Generic Location carrier, slot resolution, registration and root-chain checks |
| Type checks | Actual native dump has object fields, bounds checks and interface calls; it does not run a descriptor-catalog scan before every ordinary field access | `IsInstSealed` compares klass pointers; class tests index a type hierarchy; interface dispatch scans that class's interface-offset table with a slow fallback | Generated predicate catalogs and generic helper checks; the broad object predicate also implements checked known_type |
| Character string | `CharacterChunker.Chunk` IL_001c invokes Char.ToString; JIT allocates one string and stores its UTF-16 code unit | `Char.ToString` calls String.CreateFromChar, which allocates a one-character string and writes firstChar | `new char[]{value}` then CopyCharacters creates an int[] then FromCodeUnits allocates the string |

Roslyn `EmitExpression.cs:726` handles ref/out by calling `EmitAddress`;
`EmitAddress.cs:288` emits a local's address. No runtime borrow registry is added
there. Lifetime correctness still belongs to the compiler/runtime; this does not
justify accepting arbitrary host pointers or dropping roots for heap interiors.

Unity source anchors (full local paths and hashes in
[reference-sources.json](runtime-costs-20261020/reference-sources.json)):

- `DiffPlex.Benchmark.cpp:2613` BuildPieceHashes, `:2856` CharacterChunker.Chunk.
- `Generics__3.cpp:6967` reference-key/Int32-value Dictionary.TryGetValue.
- `mscorlib__1.cpp:20405` Char.ToString; `mscorlib.cpp:24601` String.CreateFromChar.
- Installed 6000.3.2f1 `il2cpp-codegen.h:563` sealed checks, `:576` class checks;
  `ClassInlines.h:16` hierarchy indexing, `:39` interface-offset lookup.

The original Unity input DLL hash matches ours; its stripped DLL is a distinct
artifact. Match methods by identity/body, not stripped metadata token equality.
The installed runtime source and retained October 8 output are separate evidence.
[dotnet-arm64.txt](runtime-costs-20261020/dotnet-arm64.txt) retains the actual local
non-tiered JIT output for the hot methods (excerpt with trailing whitespace
removed); it was collected outside timing.

REA 6.7.0 inspected the exact newly captured owned core as metadata, without
executing it. [rea-summary.json](runtime-costs-20261020/rea-summary.json) retains
method signatures, IL hashes, offsets and call anchors. Important examples:

- StringKeyOperations.Rotate `0600024b`: nine IL bytes; Hash `0600024d`: 108 bytes.
- Static Char.ToString `060002a2`: IL_0001 newarr, IL_000a string construction.
- Dictionary.TryGetValue `0600049f`: 34 IL bytes; IL_0012 stobj writes the out value.

## Cost counts and the residual bottleneck

A separate instrumented **cold single diff**, not used for timing, observes:

- 173,406 execution steps and 4,533 arena-frame reservations.
- 2,859,448 cumulative reserved frame bytes, repeatedly initialized/reused. This
  is not peak memory, nor a measurement of exact stores emitted by Clang.
- 29,723 predicate invocations and 127,982 descriptor comparisons.
- 40,852 root-chain visits and 8,667 borrow-chain visits.
- 539 array and 264 string allocator invocations, including initialization.
- 256 static character conversions, hence 512 temporary arrays attributable to
  that path; 283 StringKeyOperations.Hash/Pair/Rotate invocations each.

The 2.70x prototype's separate profile has 2,207 exclusive application samples:
location_storage **399 (18.1%)**, Dictionary.TryGetValue 232 (10.5%), ordinal
GetHashCode 149 (6.8%), StringKeyOperations.Hash 129 (5.8%), dictionary Equal 125,
Hash 122, HashKey 87. Inlining can move work into caller symbols; method totals
must not be treated as isolated algorithm costs. Source locations still show
borrow registration, frame/root setup and generic Location discrimination.

Thus an indexed lookup alone is insufficient: the remaining borrowed load still
constructs/copies/discriminates a generic carrier and validates properties that
the compiler often knows. Unity's direct typed address explains the relevant
architectural difference. It does not prove every Cilica borrow can use it.

Owned Dictionary entries are also separate reference objects, whereas Unity's
shown entries are inline values in an array. That adds indirection/allocations;
its isolated timing contribution is not measured here. Likewise, the remaining
small hashing/comparer methods retain general frames. **The residual 25x has not
been fully apportioned.** The evidence identifies these concrete next suspects
rather than attributing all of it to GC or one hidden check.

## Recommended production order

1. Direct native control flow for methods without local exception regions,
   preserving managed fault propagation and roots. Keep bounded diagnostic
   emission where needed; derive release stack limits from measured whole-call
   budgets. The existing O0 sanitizer spill constraint must not silently dictate
   release execution structure.
2. Direct character-string construction using existing allocator/root rules.
   Validate the full UTF-16 domain, result identity and allocation failure behavior.
3. Typed direct access for proven nonescaping frame borrows, including scalar out
   locals and private read-only string operations. Retain an authenticated general
   path where proof is absent. Preserve heap-interior roots, readonly/covariance
   semantics, stale lifetime rejection and checked public boundaries.
4. General proof-based removal of frames for nonallocating helpers, including
   hash arithmetic/comparers; measure this against the residual profile. Descriptor
   indexes remain a useful supporting optimization, not the main solution.

No instruction above authorizes changing game/library results to make a benchmark
faster. Dictionary storage changes need their own reentrancy/value semantics review.

## Reproduction, review and scope

Fresh C++ emission on this branch matches the previous v92 candidate byte for
byte. Exact owned core SHA256 is
`10a99465bbb826b4dda27ab513e4e156da93ea25030d80f2cefed778b24bb5c3`.
Core build options are Release `-optimize+`, `-debug-`, C#9; there is no core PDB.
The benchmark DLL does have its matching PDB. The only source change is an opt-in
`CILICA_BENCH_CORE_CAPTURE` path in the existing explicit emission harness, to
retain exact DLL/options/manifest before temporary cleanup. No production compiler,
runtime, library, profile or grant changes are made.

All experiment transforms are under [replay/](runtime-costs-20261020/replay/).
These are archived diagnostic scripts, **not production compiler passes**. Copy
them to a fresh ignored evidence directory, emit `baseline.cpp` there using the
approved emission harness, capture `core/`, and create `core-methods.json` with
the retained metadata reader. From the repository root run factorial.py, then
profile.py/counts.py separately, direct_cfg.py, and char_path.py. They expect the
pinned local host and earlier expected transcript; inspect the paths first. Run
no compilation/sampling concurrently with timing. The direct-CFG paired timing
uses the same rotating three-process/21-sample protocol as char_path.py.

Full generated sources, binaries, logs, raw samples, REA output and JIT output are
retained in ignored `artifacts/runtime-cost-investigation-20261020/`; compact
provenance and source hashes are versioned alongside this report.

Review limitations: B narrows private registration to the generated immediately
active frame; it is not a drop-in implementation for arbitrary native harnesses.
Token identity/retirement checks remain, but malformed-pointer and lifetime tests
have not accepted this prototype. T compares addresses before dereferencing and
retains exact membership rather than assuming every descriptor is valid. D and
CFG alter native stack/inlining behavior; recursion, wide methods and target stack
budgets require acceptance. Char construction changes allocation-failure behavior.
No general exception-region, cross-target, or public ABI compatibility is claimed.
The final combined prototype also passes the complete 523-value transcript under
O2 ASan/UBSan, plus a cold checksum run collecting before every allocation.
This validates this workload only. Its sanitizer stack-usage file reports a
largest numeric frame value of 1,536 bytes with a dynamic classification; that is
not a whole-call-chain or arbitrary-method stack bound. The explicit emission
test passes, the project build succeeds (one existing CS0108 fixture warning),
and the new capture produces the expected core hash. Source/evidence review
found no production changes to accept or revert; the limitations above are the
required acceptance work before promoting any prototype.

Hosted CI, Unity execution, external agents and pushes were not used.
