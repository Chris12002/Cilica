# DiffPlex execution improvements, 2026-10-20

Apple M4 Pro, macOS ARM64, Apple Clang 17. Each slice uses freshly emitted
candidate C++, O2/NDEBUG, C++20, no native exceptions/RTTI. Sanitizers and forced
collection are separate correctness runs. No hosted work or services are used.

Each timing comparison alternates three process pairs with 300 ms warmup and
seven samples of 256 diffs per process. All 21 samples per binary are retained.
The complete 523-value transcript must match before timing; checksum is 6146.
`tools/Performance/execution_slices.py` retains exact sources, compiler commands,
binary/source hashes, validation, raw samples and host load. Candidate source
hashes identify uncommitted implementation at measurement; final commits follow
review. No generated-method substitutions are used.

| Slice | Before µs/diff | After µs/diff | Speedup |
| --- | ---: | ---: | ---: |
| Direct native control flow, v92 → v93 | 365.333 | 243.936 | 1.4977x |
| Borrow lookup, v93 → v94 | 246.020 | 231.181 | 1.0642x |
| Direct character strings, v94 → v95 | 225.063 | 164.192 | 1.3707x |

## Direct flow

Baseline compiler df37feb (compiler implementation unchanged from b44dbc6),
independently emitted during the preceding investigation. The current branch's
v93 candidate is regenerated, including after the review correction; the two
candidate files match byte for byte. No local exception regions occur in the
144-method DiffPlex closure. All are eligible for direct flow.

[ADR 0199](../decisions/0199-direct-native-control-flow.md) records the design,
review and correctness checks. [Summary](execution-slices-20261020/direct/summary.json),
[provenance](execution-slices-20261020/direct/provenance.json),
[validation](execution-slices-20261020/direct/validation.json) and raw samples are
retained beside this report. Raw C++/binaries remain under
`artifacts/direct-control-flow-20261020`. Review checks pass 23 distinct tests.

## IL and comparator continuity

The unchanged DiffPlex DLL is SHA-256
`652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`;
its PDB is `4eac2d8ae6c2e7efaade8bca707bcbbe5673dc317cf141176fd4ce7b6ee285b8`.
The [preceding investigation](runtime-costs-20261020.md) retains Roslyn Release IL,
compiler options/version and method-token/offset tracing. BuildPieceHashes
06000011 IL_007d/007f remains a managed borrow passed to TryGetValue; Chunk
06000021 IL_001c remains Char.ToString. These source operations are unchanged by
control-flow rendering. IL2CPP emits ordinary native branches and typed byrefs;
its generated Char.ToString uses direct string allocation. Roslyn source is a
comparative revision, not an exact match to the unavailable compiler commit.
The preceding .NET non-tiered measurement is 5.303 µs/diff; Unity's 24.368 µs
measurement is historical (October 8). Neither is presented as a fresh per-slice
comparator. Current native changes still leave a substantial gap to .NET.

## Borrow lookup

Baseline de4b886, fresh v94 candidate. Paired medians are 246.020 to 231.181
microseconds/diff (1.0642x). Four full transcripts and two forced-GC
operations pass; diagnostic verification rechecks both lifetime chains.
[Summary](execution-slices-20261020/borrow/summary.json) and
[provenance](execution-slices-20261020/borrow/provenance.json) retain the details.
Raw output is in `artifacts/borrow-lookup-20261020`. Forty-five distinct focused
checks pass across the final corrected runs; retained logs show the initial host
stack-harness failure and its separate passing rerun. The 2 KiB bound is unchanged.
See [ADR 0200](../decisions/0200-cached-borrow-registration.md).

## Character construction and final comparison

Baseline cbebe58 restores exactly the v94 emitter recorded at measurement;
all compiler/core source hashes match the prior borrow provenance. Fresh v95
emission includes the newly built owned core. Twenty-five focused and 20 adjacent
checks pass; [ADR 0201](../decisions/0201-direct-character-string-allocation.md)
records the review, all-UTF-16 testing and exact allocation-failure behavior.
[Summary](execution-slices-20261020/character/summary.json),
[provenance](execution-slices-20261020/character/provenance.json) and
[source audit](execution-slices-20261020/character/source-audit.json) retain identities.
Raw outputs are under `artifacts/character-construction-20261020`.

A final paired run reuses the exact verified release binaries from the first
baseline and last candidate. It measures **362.107 → 164.204 µs/diff**, **2.2052x**
faster, a **54.65% time reduction**. This is an independent final comparison,
not multiplication of the slice ratios. Each row has its own paired baseline;
normal process variation explains the small differences between adjacent rows.
[Final samples and hashes](execution-slices-20261020/final/provenance.json) and
[summary](execution-slices-20261020/final/summary.json) are retained. No timing
measurement overlaps compiler builds or correctness tests initiated by this task.

The preceding non-tiered .NET reference remains 5.303 µs/diff, so the final
native time is approximately **31x** that reference, down from roughly 70x.
That reference was not remeasured for this slice; default tiered .NET and
historical Unity results remain distinct. This work does not close the entire gap.

Diagnostic allocation counting after timing reports **27 array allocations and
264 string allocations** per diff, versus the prior fresh v92 count of 539 arrays
and 264 strings. The 512 eliminated arrays correspond to 256 character strings.
Instrumented counting output matches checksum 6146. A separate O2 native build
with stack-usage output and the 2 KiB frame warning reports a 400-byte maximum,
all static frames. These are per-function measurements for this workload, not
an unrestricted call-chain or cross-platform stack guarantee.

The new core DLL SHA-256 is
`655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1`.
Its build options, compiler log and complete manifest are retained in `character/`.
The core build does not emit a PDB; the unchanged application DLL/PDB identities
remain those recorded above. REA inspection of the freshly compiled core finds
static Char.ToString at **060002a3**, reduced from 16 to **7 IL bytes**:
ldarg.0, call at **IL_0001** to FromCharacter **06000234**, ret. The old core's
static method was 060002a2; tokens must be interpreted with their image hash.
The [REA summary](execution-slices-20261020/character/char-il-summary.json) preserves
signatures and IL hashes. The [native excerpt](execution-slices-20261020/character/char-native.txt)
traces that method into string_from_character and the existing allocator. The
application's Chunk call at 06000021 IL_001c is unchanged. This matches the
previously inspected IL2CPP strategy of allocating one String and storing its
first UTF-16 unit, while retaining Cilica's own compiler, allocator and GC.
