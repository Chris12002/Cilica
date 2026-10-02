# Execution-frame microbenchmarks

Opt-in local measurements of the bounded object emitter's helper dispatch and
arena setup. Normal builds compile the harness; neither builds nor tests run it.
No hosted job, service, package or benchmark framework is needed beyond the
repository's .NET SDK and a C++20 compiler.

From the repository root:

```sh
dotnet build benchmarks/ExecutionFrames/Runner -c Release --disable-build-servers -m:1
dotnet benchmarks/ExecutionFrames/Runner/bin/Release/net10.0/Runner.dll run artifacts/execution-frames
```

The runner requires a new or empty output directory for each comparison, so a
failed run cannot leave old results looking current. `CILICA_CXX` selects the native
compiler (default `clang++`). Keep the machine otherwise idle. In an offline
environment with the repository's packages already cached, restore with
`-p:NuGetAudit=false --ignore-failed-sources` for that invocation, then build with
`--no-restore`; this does not change the repository's audit settings.

The runner generates and compiles C++ at O2 without sanitizers or LTO, launches
two native executables and a separate authored .NET reference process, then
checks all timed results before reporting medians, minima and maxima. Each child
process has a 60-second limit and is owned by the harness. Input methods are
never executed during metadata translation. The reference process only executes
the workload library referenced at build time; it cannot load arbitrary inputs.

## What is measured

Three nonallocating, wrapping Int32 workloads share source across all backends:
one arithmetic expression, a 128-iteration arithmetic loop, and a 128-iteration
loop with a direct managed call. Every iteration depends on the preceding result
and changes the next input. The final checksum is printed after timing; matching
the .NET checksum is mandatory for every sample. Each workload warms up for 2,000
calls and collects five samples. Arithmetic uses 2,000,000 calls per sample;
the loop uses 100,000 and the call loop 30,000.

| Backend | Timed work |
| --- | --- |
| `primitive` | Earlier Int32 emitter's status boundary; no heap execution frames |
| `object-boundary` | Packed object boundary with a fresh heap/context, arena allocation and teardown per call |
| `object-warm` | Private object method with a reused arena and a reserved two-argument packet; still checks depth, reserves/releases method frames and publishes roots |
| `dotnet` | Authored method via a delegate, in a Release process with tiered compilation and ReadyToRun disabled; JIT warmup excluded |

The warm path is a diagnostic comparison, not a supported persistent embedding
API. These workloads allocate no managed objects, so arena reuse cannot accumulate
retained objects. They isolate execution machinery; they do not characterize
object allocation, collection, reference-heavy execution or real game workloads.
Clang and the JIT may optimize/integrate primitive calls differently, including
inlining. Ratios include those differences and runtime checks; they are not a
measurement of the indirect-dispatch instruction alone.

The primitive emitter has a different storage/depth policy and is a comparator,
not a substitute for the budgeted object backend. The native timing wrappers are
non-inlined, while ordinary compiler optimization within each backend remains
enabled. Harness call/check overhead is included. Sequential process ordering,
OS scheduling and dynamic clock speeds limit precision; there are no timing
assertions or universal speed claims.

`samples.tsv` preserves every timing/checksum; `sizes.tsv` records generated
source bytes, complete executable bytes (including harness/runtime/linker
overhead) and one native compile duration. `environment.txt` records OS,
architecture, .NET/native toolchain, settings and input/compiler image hashes. Record the CPU model, memory,
source revision and local conditions with any published comparison. Build time
is a single observation, not a repeated benchmark. Cold start, peak memory,
NativeAOT, mobile/WebAssembly targets and bridge cost are outside this probe.
