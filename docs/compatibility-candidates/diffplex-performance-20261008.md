# Cilica versus .NET: bounded DiffPlex Release measurements

**Cilica is substantially slower on the one fully verified workload measured.**
For a 128-character diff, the median is **265.5 ms for Cilica versus 5.75 microseconds
for .NET**, about **46,000 times slower**. This is a small, allocation-heavy
microbenchmark of the current bounded profile, not a general application speed
estimate. Cilica varies considerably across samples; profiling is needed before
attributing the gap to a particular compiler, library or runtime component.

| Measurement | Normal .NET Release | Cilica native Release |
| --- | ---: | ---: |
| Median time per diff | 5.7515 us | 265.5011 ms |
| Seven-sample min–max | 5.6588–6.1052 us | 90.0390–457.5068 ms |
| Iterations per sample | 32,768 | 2 |
| Warmup | 52,672 diffs / 300 ms | 8 diffs / 1,509 ms |
| Process peak RSS, Linux wait4 | 37.74 MiB | 16.22 MiB |
| Median first invocation in a fresh process | 5.1121 ms | 33.2324 ms |
| Median whole cold command | 182.055 ms | 34.440 ms |

The initial .NET batches had only two operations and were too short. Their raw
results remain retained but are **not** the final .NET estimate. The longer samples
above last 185–200 ms apiece, include 111–112 gen-0 collections per sample, and use
an assembly whose SHA256 exactly matches the fully verified trial. Cilica's seven
samples last 180–915 ms apiece. Different batch counts permit meaningful timing
windows; each operation uses identical deterministic inputs. The native variation
is material: this is not a confidence interval or a stable universal multiplier.

## Workload and correctness

Both inputs are exactly 128 UTF-16 units, with two substitutions at positions 31
and 95. Only the unchanged approved character-diff root is timed. Input construction
happens before the steady-state timer. The managed loop invokes the diff, consumes
constant-size piece/block counts to retain the result, and accumulates a checked
external checksum (6146 per operation, with ordinary unchecked Int32 accumulation).
It does not serialize, traverse the full result or use an inter-runtime bridge in
the timed loop. A single ordinary entry call per batch is amortized over its
iterations. Build/JIT warmup, full verification and output serialization are outside
steady-state measurements. No sanitizer or forced collection is enabled.

Before timing, all **523 individual transcript observations**—piece counts, every
piece's length and UTF-16 units, and all block positions/counts—match normal .NET.
The transcript, input hashes, output hash and every timed checksum are retained.
The existing independent 64-scenario DiffPlex success and its 11,232 repeated O0/O2
comparisons are unchanged; they were not used as speed measurements.

A second 1,024-character line workload exceeded the 180-second subprocess bound
while validating the full native transcript. It has **no accepted performance
result**. The partial output and timeout remain recorded. No performance conclusion
is drawn from correctness-loop, verification, compilation or total trial elapsed
seconds.

## Startup and memory limits

The fresh-process first-call measurement includes first-use initialization and,
for .NET, applicable JIT work. The whole-command row additionally includes process
launch, runtime setup, result output and teardown. The .NET host uses JSON
serialization while native output uses printf; therefore that row **does not
isolate startup** and must not be used to claim a general native startup advantage.
A no-op startup baseline was not measured. Cold-call values use three fresh
processes per runtime, with alternating launch order.

Peak RSS uses the **same Linux wait4 ru_maxrss method**, converted from KiB to MiB,
for each process. It includes runtime/code, warmup and sampling, not just managed
objects. These are process high-water marks, not maximum managed allocation sizes.
.NET reports approximately 14,226 allocated managed bytes per diff using
GC.GetTotalAllocatedBytes; this is sampled outside timing. Cilica has no equivalent
total-allocation counter here. Its managed heap bytes after samples range from
75,420 to 419,132, including uncollected objects; these are neither total allocations
nor peak RSS. Do not compare those counters as equivalent quantities.

Cilica retains its default 1 MiB managed-heap/4,096-object bounds and normal
allocation-triggered collection. .NET uses its ordinary workstation collector.
Runtime policies differ and are part of this bounded-profile comparison; no Cilica
quota or runtime check was removed to improve its result.

## Reproducible pins and settings

- Compiler/library: `3d0bb53e94b5356cbbe59fae3c2ebca93af68fc8`, the revision that
  passed the approved DiffPlex correctness trial; CIL92/IR59/native79.
- Owned benchmark files: `a581334811b8f303b0869079589e0e3ce528dfb3` (initial harness
  `927df95463b52b3f2627ac22b7a3c7859c346b45`). Only those named new files are overlaid
  on the compiler export; their SHA256 hashes are recorded.
- Upstream: `mmanela/diffplex@f500e73f28e28813f942f1f16dec5a34b40e48e3`, the same 14
  unchanged approved files. No upstream project/target/generator/test is executed.
- .NET SDK 10.0.401, runtime 10.0.12; Release optimized netstandard2.0 adapter and
  net10.0 host. `DOTNET_TieredCompilation=0`, `DOTNET_ReadyToRun=0`,
  `DOTNET_gcServer=0`, `DOTNET_PROCESSOR_COUNT=2`. Warmup executes every measured
  path before sampling; tiered/PGO-default behavior is not claimed.
- Clang 18.1.3: `-std=c++20 -O2 -DNDEBUG -fno-exceptions -fno-rtti -Wall -Wextra
  -Werror`; no sanitizers. Cilica's ordinary runtime validations remain.
- Ubuntu 24.04 ARM64 guest on Apple M4 Pro (Mac16,7, 14 logical host CPUs), Docker
  quota 2 CPUs, 8 GiB/no extra swap, 256 processes, 10 GiB scratch, 1 GiB output, 30-minute
  host deadline. CPUs are quota-limited, not dedicated/pinned physical cores.
- Offline, UID/GID 10001, read-only root, no host mounts/credentials/sockets,
  capabilities dropped and no-new-privileges. Existing verified tooling image:
  `sha256:68b78e3c1c62c8ed6db8421d6f11cd0619829bd8b77ad1368cae9328e3d40f80`.

The first setup on the later 38717eb library base failed before timing because the
strict netstandard2.0 mapper could not resolve newly included CollectionExtensions.
The retry explicitly pinned the proven 3d0bb53 base; it was not an unrecorded source
change. Attempt times were 41.4s (setup failure), 375.9s (valid character data plus
line verification timeout), and 10.5s (longer .NET samples). All clocks are stopped.
All trial evidence, prior failures, exact commands and source/package pins remain
in `artifacts/diffplex-performance-20261008`; selected evidence is committed beside
this report. [Summary](diffplex-performance-20261008/summary.json) and
[cleanup receipt](diffplex-performance-20261008/cleanup.json) are the resume points.

All three containers, the two disposable input images and task source/archive
copies were removed after retaining evidence. The verified tooling image remains.
Review milestone 6 was coordinated with; its implementation work is untouched.
CRC32.NET was inspected online only and remains unapproved for execution. Its
separate approval proposal is the next workflow task, paused for this benchmark.
