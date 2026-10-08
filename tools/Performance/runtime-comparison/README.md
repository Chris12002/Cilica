# Proposed runtime comparison kernels

These independently authored inputs are **uncompiled and unexecuted**. They are
not registered in a project or CI suite. See the [read-only architectural
comparison](../../../docs/performance/runtime-comparison-20261008.md) for scope
and the decisions required before implementation or external execution.

## Inputs and exact output oracle

Use `Setup(H, s)` with `H` in `{8, 64, 512, 2048}`, seed `s` in
`{-3, 0, 7, Int32.MaxValue}`, and rounds `n` in `{0, 1, 8, 64, 512, 2048}`.
For `Rewrite`, supply a preallocated `Cell[]` of length 8 or 64. All arguments
are valid; invalid-argument behavior is not the purpose of these kernels.
Interpret the formulas below modulo 2^32 and return signed Int32. Let
`T = n * (n - 1) / 2`, calculated in a sufficiently wide oracle integer.

| Kernel | Expected result | Intended inspection |
| --- | --- | --- |
| `ReadOldest` | `n*s` | Nonallocating reference validation; check whether the loop/read is hoisted or folded. |
| `Churn` | `T` | Source-level allocation and publication; record actual allocations and ownership work. |
| `HoldEightAliases` | `T + 8*s` | Alias roots surviving a call; inspect whether locals collapse to one location. |
| `HoldEightDistinct` | `T + 8*s + 8*H - 36` | Eight distinct references across a call; inspect inlining and actual live locations. |
| `Rewrite` | `n*s + ceil(n/2)*(H-1)` | Reference stores, validation and barriers without source-level allocation. |
| `InlineRoot` | `T + s + 17` | A traced reference leaf in an inline value across a call. |

The state deliberately retains the whole setup graph. These shapes inspect root
publication costs; they do **not** prove isolated local-root survival or earlier
reclamation. Use the existing native GC/borrow/exception suite for those claims.
Do not assume source locals imply physical roots or source `new` implies surviving
allocation. If optimization removes the intended work, report it; a separate
no-inline diagnostic variant must not silently replace the clean comparison.

## Future execution protocol

1. Pin this source and each wrapper, compiler/core/runtime revision, target and
   architecture, optimization/linker flags, GC settings, quota values and binary
   hashes. Record actual NativeAOT publishing settings separately from ordinary
   .NET JIT Release. Record Unity editor/player version, backend, build mode,
   target and GC mode; a Unity engine process has additional startup costs.
   No package installation or new external execution is authorized by this file.
2. Build and inspect emitted C++/machine code and available root metadata first.
   Record actual allocation sites, inlining, root locations, safe points and
   barrier calls. Check Cilica admission rather than assuming these proposed
   kernels are supported. Do not alter semantics to make admission succeed.
3. Verify every result against the formula and .NET oracle outside timing, plus
   state mutations: setup links/values, final `Scratch` for positive `Churn`
   rounds and the final contents of rewritten slots. Zero rounds preserve prior
   state. Use fresh state per fresh-process trial; keep state and slots rooted.
4. Separate process launch, setup/first call, warmed steady compute and end-to-end
   measurements. Initialize setup and slots outside steady timing; emit results
   after the timer stops. Warm up each runtime and report warmup policy and
   elapsed duration. Calibrate batches to timer resolution, retain raw samples
   and checksum every batch. Alternate runtime order over fresh processes in an
   agreed quiet host window. No ratio is a gating test without a stable baseline.
5. Keep clean timing separate from diagnostic counters, sanitizer runs, forced
   collections and sampling. Count allocations/charged bytes, ownership queries,
   list visits, root slots and GC work. Measure survivors after explicit untimed
   collection separately from the uncollected heap phase. Repeated `Churn` calls
   overwrite `Scratch`; keep that state transition consistent across runtimes.
6. Report memory by scope: current/peak charged managed bytes, separately charged
   scratch, runtime/native overhead and post-exec process observations. `wait4`
   peak RSS is launch-inclusive and can inherit a launcher's high-water floor.
   Sampling is a lower bound on peaks, not an exact allocator-footprint measure.

Safe C# cannot reproduce foreign/interior pointers, detached allocation headers,
corrupted inventories or stale native handles. Preserve the owned native boundary
suite and exact fault/restore checks independently; NativeAOT or Unity output is
not an oracle for Cilica's additional ownership and resource contracts.
