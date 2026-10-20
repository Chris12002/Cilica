# DiffPlex section timings across Cilica, .NET and Unity

2026-10-20, committed compiler/core baseline `142aecd`, native v115. This is
diagnostic instrumentation and measurement, not a performance optimization.
Fresh native emission is byte-identical to the accepted split-cache C++.
Compiler, runtime, owned library and approved benchmark/player inputs are unchanged.

## Outcome

Line chunking is the largest remaining absolute gap: **42.35 µs/diff in Cilica,
4.54 µs in .NET and 16.00 µs in Unity**. It accounts for approximately 37.82 µs of
the instrumented gap to .NET and 26.35 µs of the gap to Unity. That favors
investigating separator matching and direct substring construction first for the
line workload.

Character hash preparation and chunking are both major targets: approximately
7.13 and 6.73 µs of excess time versus .NET, respectively. Their small difference
does not establish a robust ordering. Cilica already chunks characters faster
than this Unity player, but remains slower in hash preparation and the diff
algorithm. The diff algorithm has a distinct 3.50 µs excess over .NET on
characters and 2.19 µs on lines; its high relative slowdown should not obscure
the larger absolute chunking/hash gaps.

All section values below are exclusive elapsed-time medians in µs per diff.
Hash preparation excludes chunking. Setup/results contains the remainder inside
`BenchProbe.Diff`. GC pauses are attributed to whichever section contains them;
these are not isolated CPU or allocation measurements.

| Character section | Cilica | .NET | Unity IL2CPP |
|---|---:|---:|---:|
| Setup and result construction | 1.16 | 0.16 | 0.99 |
| Chunking | 7.63 | 0.91 | 16.17 |
| Hash preparation | 11.38 | 4.25 | 8.81 |
| Diff algorithm | 3.70 | 0.20 | 0.58 |

| Line section | Cilica | .NET | Unity IL2CPP |
|---|---:|---:|---:|
| Setup and result construction | 0.95 | 0.13 | 1.08 |
| Chunking | 42.35 | 4.54 | 16.00 |
| Hash preparation | 9.37 | 2.37 | 8.31 |
| Diff algorithm | 2.33 | 0.14 | 0.42 |

Separate medians need not sum exactly to the median total. Each individual
sample checks that its summed exclusive sections do not exceed elapsed time.
The tiny outer adapter/checksum remainder is retained in the raw summaries.

## Timer disturbance and whole-run controls

| Workload/backend | No scopes | Scopes disabled | Scopes enabled |
|---|---:|---:|---:|
| Character / Cilica | 23.723 | 23.650 | 23.907 |
| Character / .NET | 5.447 | 5.411 | 5.524 |
| Character / Unity | 25.809 | 27.335 | 26.621 |
| Line / Cilica | 54.087 | 54.883 | 55.288 |
| Line / .NET | 6.909 | 6.971 | 7.258 |
| Line / Unity | 25.613 | 25.817 | 25.764 |

Enabling timers versus the disabled-scope variant changes the overall medians
by +1.09%/+0.74% in Cilica and +2.09%/+4.11% in .NET (character/line).
Unity shows -2.61%/-0.20%, which is variation, not evidence that timers speed
execution. Disabled Unity character scopes are 5.91% slower than the no-scope
control: code generation/layout and variation matter as well as clock cost.
Enabled versus no-scope totals differ by about 0.6–5.1% across these cases.
Do not use section measurements to claim sub-percent improvements or subtract
a fixed per-timer correction.

These controls support the broad ranking, whose main differences are several
microseconds to tens of microseconds. The first .NET line instrumented process
was slower than the later two (7.83, 7.08, 6.98 µs medians); all three are retained.
No best-run selection or follow-up reruns were used.

The new no-scope Unity player also measures the previously missing line workload:
Cilica is about **2.11× slower than Unity on lines**, while character diffs remain
about **8.1% faster than Unity** in this batch. Cilica takes about
4.36×/7.83× the .NET time on characters/lines. These are local workload
comparisons, not general compiler or library rankings.

## Equivalent boundaries and build identity

Roslyn compiles the C# input; .NET and IL2CPP execute it. Both comparator players
use the same instrumented managed DLL
`87e254ab0822c3d4737f81bf8c7e9642cfd1bc77453fab73a437f5add1bace10`.
The no-scope control DLL is
`ad412e7552d1178d9915615f4c99a38edde7425f15f4c8335a199e4cc40bfd2a`.
It retains the shared runner/timing declarations but no inserted scopes, so it
is source-equivalent in the selected workload methods, not byte-identical to
the approved DLL. Fresh ILSpy disassembly confirms identical IL instructions
and resolved operands in all five selected original/control methods. Full outputs
are verified against the approved benchmark.

The approved original DLL remains
`652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`.
The native input and core are freshly emitted from the current checkout; the
diagnostic native copy adds `steady_clock` RAII scopes at the corresponding
generated method entries. This does not add Stopwatch support or special-case
DiffPlex inside Cilica. Native frames, rooting and cleanup stay inside the scope.

| Scope | Original method token | Calls/diff | Treatment |
|---|---|---:|---|
| BenchProbe.Diff | 06000053 | 1 | Outer setup/results scope |
| CharacterChunker.Chunk or LineChunker.Chunk | 06000021 / 06000029 | 2 | Chunk construction including allocation/collection |
| Differ.BuildPieceHashes | 06000011 | 2 | Child chunk time excluded |
| Differ.BuildModificationData(two arguments) | 0600000f | 1 | Full algorithm; recursion not separately timed |

Fresh metadata inspection records the new managed tokens, signatures, RVAs and
IL sizes. In the new assemblies BenchProbe.Diff is 06000058; native instrumentation
correctly uses the original 06000053. Five method bodies receive scopes. The four
original library methods keep their tokens. Scope counts `[1,2,2,1]` are asserted
per diff in every timed sample, proving all intended boundaries execute.
Selected Release IL for original, control and instrumented methods is retained,
along with generated Unity C++ boundary excerpts. The instrumented IL contains
the expected scope constructor and finally region in each selected method.

The managed scope is a value type with try/finally disposal, using
`Stopwatch.GetTimestamp` and preallocated counters. Native scopes use equivalent
nested accounting. No strings, logs or heap objects are created per scope.
The instrumentation is single-threaded and benchmark-only. Try/finally and RAII
preserve scope balance on exits; no library calls or fault checks are removed.

Builds use Release optimized C# from the same selected-source recipe, .NET
10.0.11 with tiering/ReadyToRun disabled, and native Apple Clang 17 C++20
O2/NDEBUG with exceptions/RTTI disabled. Local Unity 6000.3.2f1 players use
Release/OptimizeSpeed, Minimal stripping, incremental GC, development off and
the existing architecture setting 1. The player contains arm64/x86_64 code and
was launched normally on the Apple M4 Pro host. Both player settings are retained.
Unity CLI 1.0.0-beta.13 drove bounded local builds. No hosted CI ran.

## Validation, review and follow-up

All **18 backend/variant/workload combinations** match the complete .NET oracle:
523 character and 2,061 line observations, **23,256 compared observation values**.
Each timed run also checks the workload checksum, balanced scopes, expected
entry counts and nonoverlapping accumulated time. Three processes per combination,
seven samples each, produce **378 timed samples**. Each process warms for at
least 300 ms. Orders rotate/reverse; all measurement is sequential and follows
build completion. Iterations/sample are Cilica 1,024, .NET 8,192, Unity 4,096.
These measurements did not overlap sampling, builds or other tests.

Review checked source/token correspondence, no duplicate recursion accounting,
child subtraction, fault cleanup, no per-scope allocation, controls, checksums,
output parity, retained slow samples and original-input preservation. The initial
managed preparation hit inherited central package policy; explicit empty
Directory.Build/Packages files isolated the standalone recipe before the
successful build. No production change or semantic capability is claimed, so
existing optimization acceptance checks were not unnecessarily rerun.

Next:

1. Investigate direct UTF-16 substring construction and the remaining separator
   loop for lines. The previous owned-core profile already identifies matching,
   CopySubstring and allocator traffic inside this large section.
2. For characters, investigate dictionary/hash preparation and character-string
   creation together; use native/IL inspection to separate checks, hashing,
   equality, storage and allocation before choosing a change.
3. Inspect generated array/control-flow work in the diff algorithm. Its
   roughly 17–18.5× relative gap to .NET is measurable, but the absolute opportunity is
   smaller than the above sections.

Each proposed optimization still needs an uninstrumented before/after release
benchmark and correctness/shared-module checks. The stopwatch results locate
regions; they do not prove which internal mechanism causes each gap.

[Summary](section-timing-20261020/summary.json),
[complete-output checks](section-timing-20261020/correctness.json),
[method metadata](section-timing-20261020/boundary-metadata.json),
[control IL equivalence](section-timing-20261020/control-il-equivalence.json),
[measurement provenance](section-timing-20261020/measurement-provenance.json),
[native provenance](section-timing-20261020/native-provenance.json) and
[reproduction tools](../../tools/CompatibilityCandidates/diffplex-benchmark/sections/README.md)
are committed. Full source copies, generated C++, players and Unity build logs
remain under `artifacts/section-timing-20261020`. The original player, its DLL/PDB,
and the primary checkout's compiler files were not modified.
