# Fresh DiffPlex measurement on the latest compiler branch

Recorded 2026-10-19. This record replaces the retained-code replay as the basis
for current compiler performance decisions.

The subsequent [internal-reference optimization](internal-references-20261019.md)
is now the latest result: 15.481 ms to 0.533 ms in a new paired run. This record
retains the preceding allocation-verification evidence.

## Branch and input identity

The most recent compiler branch at the start of this work was
`codex/alchemy-replay-baseline`, revision
`b8918ac25f06d103ac8acbb74fd19045d2e06ea6` (2026-10-19), native profile 90.
It contains 73 commits beyond its common ancestor with the older performance
branch. The original milestone-7 branch had a newer commit timestamp only because
the performance work had just been committed there; it did not contain the latest
compiler/library capabilities.

`codex/runtime-performance` starts at `b8918ac`, then carries the performance
policy in `eaa5adf` and allocation-verification change in `8b0f018`. Existing
branches/worktrees are preserved. Documentation conflict resolution retained the
latest branch's compatibility history instead of importing unrelated mobile SDK
records. No compiler/library feature merge from the older branch was performed.

The managed workload remains the same reviewed DiffPlex assembly, SHA-256
`652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8`, from the
approved source pin `f500e73f28e28813f942f1f16dec5a34b40e48e3`. Keeping the managed
input fixed isolates changes in Cilica; it does not reuse old generated native
code. The owned core library is rebuilt by the current compilation fixture.

Both native C++ files were emitted independently with the current compiler:

- Baseline: `b8918ac`, before applying the allocation-verification change.
- Candidate: the compiler/runtime source committed as `8b0f018`.

Both headers identify `object-frames-native-v90`. Comparing the complete files
confirms that only the two runtime string blocks (`DiagnosticIncludes` and `Text`)
change. Generated methods, descriptors and owned-library identities match exactly.
The old October 8 native output is not used in either binary.

## Validation

The latest branch passes 82 focused checks plus seven separate-translation-unit
checks: **89 targeted tests**. These include both allocation verification modes,
O0/O2 ASan/UBSan, collection/handle behaviour, strings/arrays, static storage,
exceptions, frame limits and shallow clones. The two explicit emission runs also
pass separately. The initial no-restore build exposed stale package metadata from
the previous branch; a restore from the retained local feed resolved it, and the
subsequent build succeeded.

The fresh baseline, candidate, verification-enabled candidate and sanitized
candidate each match all 523 transcript observations and checksum 6146. Separate
forced-collection operations pass in verification and sanitized normal modes.

## Fresh result

Apple M4 Pro, macOS 15.7.3 ARM64, Apple Clang 17, C++20/O2/NDEBUG, no RTTI,
exceptions or sanitizers in timed binaries. Each variant has three fresh
processes, seven samples per process and eight operations per sample. Warmup is
at least 300 ms, order alternates, and input construction/output traversal stay
outside timing. Tests, compilation and diagnostics finish before timing.

| Fresh profile-90 build | Median per operation | Observed range | Samples |
| --- | ---: | ---: | ---: |
| Baseline `b8918ac` | 143.511 ms | 120.395–164.283 ms | 21 |
| Allocation verification diagnostic only | 15.350 ms | 13.263–16.251 ms | 21 |

The paired median improvement is **9.349x**. All 42 timed checksums match.
Host load is recorded and nonzero; no samples are discarded. The previously
reported 9.237x was the historical replay and is not used to calculate this result.

The same-session ordinary .NET comparator for this identical managed DLL measured
5.106 us/op ([raw comparator record](allocation-verification-20261019/dotnet-summary.json)).
It was not rerun for this branch change. The current candidate remains roughly
3,000x slower, so competitive execution remains outstanding. Unity was not rerun;
its historical 24.368 us/op is reference context, not a fresh comparison.

## Evidence and review

[Retained evidence](latest-diffplex-20261019/) includes input/library-tree identity,
compiler DLL and emitted-source hashes, native commands/binary hashes, full
transcripts, stress outputs, all timing samples, host load and test/emission logs.
Generated C++ and binaries remain in the ignored local directory
`artifacts/latest-runtime-performance-20261019`.

Review verified ancestry from the latest compiler tip, preserved branch-specific
capability documentation, exact runtime-only source differences, unchanged
managed input/library sources and checksum-validated raw medians. The runner now
has a `prepare-fresh` path and rejects even an empty inherited forced-GC setting
before timing, because C++ `getenv` treats presence as enabled. The recorded run
had no such setting. There is no new production runtime change beyond the
allocation-verification slice.

## Reproduction

Build the tests at the baseline and candidate revisions using the matching
restored dependencies. At each revision, emit to a distinct absolute destination:

```sh
CILICA_APPROVED_DIFFPLEX_PIN=f500e73f28e28813f942f1f16dec5a34b40e48e3 \
CILICA_DIFFPLEX_ASSEMBLY=/path/to/DiffPlex.Benchmark.dll \
CILICA_BENCH_TEMPLATE=/path/to/checkout/tools/CompatibilityCandidates/diffplex-benchmark/native-host.cpp.template \
CILICA_BENCH_CPP=/absolute/path/to/baseline-or-candidate.cpp \
dotnet tests/Cilica.Metadata.Tests/bin/Debug/net10.0/Cilica.Metadata.Tests.dll \
  -explicit only \
  -method Cilica.Metadata.Tests.ApprovedDiffPlexBenchmarkEmissionTests.Emit_approved_benchmark \
  -culture invariant -noColor
```

On the candidate branch, compile and compare the two independently emitted files:

```sh
python3 tools/Performance/allocation_verification.py prepare-fresh \
  --source /absolute/path/to/baseline.cpp \
  --candidate-source /absolute/path/to/candidate.cpp \
  --baseline-revision b8918ac \
  --expected docs/performance/allocation-verification-20261019/expected.json \
  --output /tmp/cilica-fresh-diffplex
python3 tools/Performance/allocation_verification.py validate \
  --output /tmp/cilica-fresh-diffplex
python3 tools/Performance/allocation_verification.py measure \
  --output /tmp/cilica-fresh-diffplex
```

Preparation rejects differences outside the intended runtime blocks. It adds an
identical optional forced-GC switch to the test host in both files, outside timing;
production generated methods remain untouched. Validation and timing verify
prepared artifact hashes. Compilation flags, complete emitted-source hashes and
raw results are retained. No hosted work, services or additional agents are used.
