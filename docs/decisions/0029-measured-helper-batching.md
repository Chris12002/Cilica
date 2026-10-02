# ADR 0029: Dispatch benchmarks and rejected larger batches

Status: retain the two-statement baseline; reject the four-statement candidate.
Recorded 2026-10-02.

## Context and decision

ADR 0027 used two ordinary IR statements per non-inlined native helper to bound
unoptimized compiler spills. ADR 0028 added wide-export, arena-lifetime and
whole-chain diagnostics. Neither checkpoint measured dispatch costs.

Retain two ordinary statements per helper and four-copy argument/parameter/edge
batches. A four-statement candidate reduced generated code for the nonallocating
probe but failed native-stack validation for reference-field operations at O0
with ASan/UBSan. The measured frames were 2,304, 2,496 and 2,576 bytes, above the
existing 2 KiB ceiling. Reject and revert the candidate without weakening the
threshold. An IR statement count alone does not capture ownership/type checks
and sanitizer spill costs. Future batching needs a cost model or separate
boundaries for expensive reference operations, followed by full stack validation.

Deferred follow-up: this optimization is not a prerequisite for continuing the
object-runtime milestones. Keep the safe baseline while adding capabilities.
Revisit batching after representative workloads establish a bottleneck on a
quiet host; distinguish arithmetic from reference checks, retain O0/O2 sanitizer
and whole-chain stack gates, and validate every newly supported target separately.

Follow-up decision: defer throughput tuning until representative applications
run. The 2 KiB warning threshold is a conservative tested baseline, not a
permanent architecture requirement. The experiment exceeded that baseline;
it did not establish that a 4 or 8 KiB threshold is inherently unsafe or a poor
tradeoff. A larger frame retained across managed calls accumulates through the
depth limit; a leaf helper has a different whole-chain cost. Reconsider the
threshold together with target thread-stack budgets, measured call-chain use,
sanitizer overhead and reproducible speed gains. Do not let an O0 sanitizer
frame measurement alone dictate the eventual production throughput design.

The existing native frame-budget regression caught this failure; no new test
merely duplicating it is needed. No production compiler/runtime change is retained.

Add an opt-in [execution-frame benchmark](../../benchmarks/ExecutionFrames/README.md).
It compiles authored arithmetic, loop and direct-call workloads through both
emitters, with a separate .NET reference process. Compare fresh object-boundary
calls and a private reused arena. Every timed sample must match the reference
checksum. Normal builds/tests do not execute benchmarks; no hosted work is added.

## Local measurements

Apple M4 Pro, 48 GiB RAM, macOS 15.7.3 ARM64, Apple Clang 17
(`clang-1700.4.4.1`), .NET 10.0.11 / SDK 10.0.400. Native O2, no LTO or sanitizers;
Release .NET reference with tiered compilation and ReadyToRun disabled. Five
samples per workload/backend after 2,000 warmup calls. The baseline compiler is
commit `c7a9147`; the candidate differs only in the statement batch constant.

| Three-workload object translation unit | Two statements | Rejected four-statement candidate |
| --- | ---: | ---: |
| Generated step helpers | 50 | 37 |
| Generated C++ including benchmark harness, bytes | 58,926 | 56,032 |
| Complete executable including harness/linker overhead, bytes | 53,368 | 48,968 |

The rejected candidate had 26% fewer step helpers, 4.9% less source and an 8.2%
smaller executable for this probe. These savings are not in the final compiler.
The unchanged primitive translation unit remains 17,415 source bytes and 34,552
executable bytes.

Median time per call in microseconds (a loop call performs 128 iterations):

| Workload | Object boundary, before → after | Reused arena, before → after |
| --- | ---: | ---: |
| Arithmetic | 0.364 → 0.103 | 0.211 → 0.033 |
| Loop | 10.637 → 8.713 | 15.992 → 13.077 |
| Direct-call loop | 9.636 → 24.645 | 9.497 → 9.867 |

These timings are inconclusive. The unchanged primitive loop median moved from
0.504 to 2.311 microseconds, and its direct-call loop from 0.863 to 4.429.
Within-run ranges were also large. Sequential process ordering, host contention
and clock/scheduling variation were uncontrolled; do not attribute a particular
speedup or the apparent call-loop regression to batching. A quieter paired
experiment is required before throughput conclusions. Runs were not repeated
to select a favorable result.

Both runs validated all 60 timed checksums against .NET. Ignored local output
directories `artifacts/execution-frames-baseline` and
`artifacts/execution-frames-four-statements` retain generated C++, binaries, raw
samples and environment records. The reproducible harness is committed; the
machine-specific artifacts are not. Native compile durations are single,
environment-sensitive observations, not build-performance evidence.

The probe covers nonallocating Int32 programs emitted by the object backend.
Arena reuse is a private diagnostic path, not a persistent embedding API.
Managed allocation, reference-heavy execution, cold start, bridge overhead,
NativeAOT, peak memory and other targets remain unmeasured.

## Validation and review

The four-statement candidate passed 419 of 420 targeted cases in 63.0 seconds;
the O0 native frame-budget case failed the enforced 2 KiB warning as described
above. Wide finite/mutual recursion alone passed, demonstrating why arithmetic
probes cannot replace reference-operation coverage.

After reverting, compiler/runtime and test sources exactly match `c7a9147`, whose
420-case validation is recorded in ADR 0028. Both focused native frame-budget
cases pass again in 17.6 seconds. Test and Release benchmark builds have zero
warnings/errors. The initial restore could not contact NuGet's audit service;
the local cached-package build used `NuGetAudit=false` for that invocation only.

Review checked wrapping input arithmetic in both harnesses, observed checksums,
excluded warmup/JIT work, arena ownership and cleanup, isolated .NET execution,
invariant result formatting and test-owned subprocess time limits. The harness
rejects nonempty output directories to prevent stale results after failed runs.
No timing threshold is a correctness test. Local main and cached origin/main
remain ancestors; no push, hosted job or additional agent was used.
