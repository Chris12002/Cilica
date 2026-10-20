# DiffPlex section timing

Diagnostic tools for comparing equivalent character/line workload stages across
Cilica, ordinary .NET and Unity IL2CPP. They do not modify compiler/runtime source
or the approved benchmark/player. Use a separate output directory.

The four exclusive categories are setup/results, chunking, hash preparation and
the diff algorithm. `BenchProbe.Diff` provides the outer scope. `BuildPieceHashes`
contains the chunker scope, whose time is subtracted from hash preparation.
Only the two-argument, outer `BuildModificationData` is instrumented; recursive
calls remain inside that scope. Expected entries per diff are `[1,2,2,1]`.

Managed copies use `Stopwatch.GetTimestamp` with stack-only disposable scopes.
Cilica emits the original approved DLL through the current compiler and owned
core, then a diagnostic copy receives equivalent native RAII scopes. The source
instrumentation is not a new Cilica capability. Both record elapsed time,
including collection occurring inside a section. They do not separately isolate
hash computation, dictionary probes, allocation or GC.

Three variants distinguish timer effects:

- `plain`: no inserted scopes. The managed assembly still contains the unused
  timing type and shared runner; it is not byte-identical to the approved DLL.
- `off`: scopes compiled in, timers disabled.
- `on`: scopes compiled in, timers enabled.

Use whole-run `plain` results for baseline comparisons and `on` sections for
diagnosis. Do not subtract a universal timer calibration: code layout, inlining,
collection and sampling variation also affect these controls.

From the compiler checkout, with the approved test assembly and its source
recipe already available:

```sh
python3 tools/CompatibilityCandidates/diffplex-benchmark/sections/prepare.py \
  artifacts/section-timing-YYYYMMDD /absolute/inputs/candidate /absolute/UnityProject
python3 tools/CompatibilityCandidates/diffplex-benchmark/sections/prepare-native.py \
  artifacts/section-timing-YYYYMMDD /absolute/inputs/dll/DiffPlex.Benchmark.dll
python3 tools/CompatibilityCandidates/diffplex-benchmark/sections/prepare-control.py \
  artifacts/section-timing-YYYYMMDD /absolute/inputs
python3 tools/CompatibilityCandidates/diffplex-benchmark/sections/build-unity.py \
  artifacts/section-timing-YYYYMMDD sections
python3 tools/CompatibilityCandidates/diffplex-benchmark/sections/build-unity.py \
  artifacts/section-timing-YYYYMMDD control
python3 tools/CompatibilityCandidates/diffplex-benchmark/sections/measure.py \
  artifacts/section-timing-YYYYMMDD
```

The native preparation deliberately asserts exact equality with committed
`142aecd`'s retained split-cache emission. A later compiler/core requires an
explicitly reviewed baseline update. The metadata token map is specific to the
hash-pinned approved input; never transfer tokens to a different DLL.
The scripts rely on the existing opt-in emitter test executable being built from
the checkout. They compile only approved workload copies and preserve original
inputs. Unity builds are local bounded batch commands, not persistent services.

Run all builds and other tests before measurement. The supervisor verifies all
523 character and 2,061 line observations for every backend/variant, then runs
three processes per variant/workload with seven samples each and at least 300 ms
warm-up. Orders rotate/reverse. Checksums, scope counts, scope balance and
nonoverlapping time totals are checked. Results and complete commands are saved
alongside hashes/settings. .NET tiering and ReadyToRun are disabled.

The first accepted investigation and its review are recorded in
[`docs/performance/section-timing-20261020.md`](../../../../docs/performance/section-timing-20261020.md).
