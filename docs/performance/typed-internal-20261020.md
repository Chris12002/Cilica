# Typed internal helpers and proven borrows

2026-10-20; Apple M4 Pro, macOS ARM64, Apple Clang 17, .NET 10.0.11.
Implementation commits: `f23182c` (stack-budget review), `42ba47b` (native v96
scalar kernels), `788760a` (native v97 stable borrows and helper chains).

## Result

The final independently paired DiffPlex measurement improves from **169.081 to
142.415 microseconds per diff**, **1.1872x faster / 15.77% less time**. Fresh
non-tiered .NET measures **5.180 us**, leaving **27.49x**. This does not close the
remaining performance gap. Unity was not rerun; the structural comparison from
[the investigation](remaining-gap-20261020.md) guided these changes.

| Slice | Paired baseline us | Candidate us | Time reduction |
| --- | ---: | ---: | ---: |
| Scalar native helpers | 167.498 | 163.697 | 2.27% |
| Stable borrows and direct proven helper calls | 169.551 | 142.711 | 15.83% |
| Final v95 versus committed v97 | 169.081 | 142.415 | 15.77% |

Each row is its own sequential series; do not compound the rows. The scalar
slice's ranges overlap and its small gain should be treated accordingly. Final
baseline range is 163.742–193.793 us; final candidate range 137.707–144.013 us.
Every series retains all 21 samples per variant, including outliers.

## What changed

The old emitter rollback was saved and restored to v95. Fresh baseline emission
was byte-identical to the previously accepted v95 artifact. The 2 KiB native-frame
warning is now explicitly a historical screening rule. A separate release target
check runs with a 4 KiB review threshold on a requested 512 KiB pthread stack,
checking actual macOS size within 512–576 KiB. Original O0/O2 constrained-stack
checks for the conservative arena fallback remain. Per-frame size does not
replace complete deep-call execution. See [ADR 0202](../decisions/0202-release-stack-budgets.md).

Bounded scalar methods now use typed arguments and native locals. Direct calls
avoid general argument packets. A later extension resolves proven read-only uses
of scalar/reference borrows once per call and keeps the snapshot in native locals.
Acyclic calls between proven kernels stay native, including the string hash's
Pair/Rotate chain. No hash algorithm or owned library source changed. Ten methods
in the DiffPlex closure receive kernels, including StringKeyOperations.Hash.

The proof excludes allocation, callbacks, mutations outside local variables,
handlers, indirect calls, recursive kernel graphs, address escape, narrow borrows
and forwarding borrows between kernels. Existing caller roots retain references.
Exact carrier checks remain in place, including stale/foreign token, readonly,
width and predicate validation. Cold/fault paths retain the old framed body;
speculative failure has no side effects and replays there for original fault
origins and managed exception materialization. The 64-call count is preserved.

See [ADR 0203](../decisions/0203-typed-scalar-kernels.md) and
[ADR 0204](../decisions/0204-proven-borrow-kernels.md). This is selective native
storage, not moving every arena frame onto the native stack. The largest measured
O2 DiffPlex native frame remains **400 bytes**. More stack is available for future
measured decisions; this slice did not need to consume that allowance.

## Correctness and stack verification

- Fourteen initial checks validated reconciliation, native control flow, character
  construction and the separate release-stack budget.
- Nineteen distinct scalar-slice checks passed across its final runs, including
  forced fast/fallback modes, O0/O2 arithmetic/loops/wide values, caught faults,
  zero-arena nested calls, pending/cold/depth fallbacks and deep generated calls.
- The final focused set has **76 passing checks**, including all thirteen authored
  typed-helper/borrow cases. It covers aliases, mutation between calls, string
  null/bounds faults, UTF-16, stale/foreign/readonly/wrong-width carriers,
  dictionary callbacks and mutation, pinned hash lengths 0–64 and code-unit
  boundaries, module linkage, both stack profiles and cleanup.
- Every release benchmark variant matches **all 523 .NET output observations**
  and checksum 6146 per diff before timing.
- After committing, the complete final benchmark separately matches all 523
  observations on a 512–576 KiB pthread stack in O2 release and O1 ASan/UBSan.
  The latter forces collection before every allocation and enables both internal
  reference and borrow-lifetime diagnostics. Both force direct flow and typed
  kernels; neither reports sanitizer errors.

These are macOS ARM64 target results, not new mobile/WebAssembly compatibility
or a universal host-stack guarantee. The explicit fast-path sanitizer screen is
16 KiB per function, separate from the 4 KiB release screen; full constrained
thread execution remains required. Existing conservative diagnostic tests keep
2 KiB where appropriate. The build retains one unrelated existing CS0108 fixture
warning; no new source warnings remain.

## Measurement identity and replay

All native timing uses C++20 O2/NDEBUG, no native exceptions/RTTI, and no sanitizers.
Three processes per variant each warm for at least 300 ms and measure seven
samples of 256 diffs. Orders alternate/reverse. No build, profiler or test runs
concurrently with timing. .NET uses three processes and 16,384 diffs per sample,
with tiering and ReadyToRun disabled to match the historical gap. NativeAOT is not
used. Benchmark timing is host-inclusive and scoped to the retained workload.

Final C++ SHA-256:
`20c186d15788437cca3848f4d9238e5c7f97e815ec40d016c7957bef90bba0c2`.
Owned core remains
`655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1`.
Input DLL/PDB hashes, exact commands, compiler-file hashes and sample data are
retained in [the evidence folder](typed-internal-20261020/source-identity.json).

An unexplained writer restored the emitter to HEAD again during development.
The already-built v97 compiler continued running, so that first filesystem-source
snapshot was not accepted as build provenance. After restoring and committing the
intended emitter, a fresh build/regeneration reproduced the measured C++ byte for
byte. All recorded v96 and final v97 compiler source hashes match their respective
commits. A later check caught another overwrite of the same two emitter lines;
they were restored again from the verified commit. The writer's cause remains
unknown, so reconciliation is not a claim that future overwrites are prevented.

Large generated sources, binaries, core captures and full native logs remain in
`artifacts/typed-internal-20261020`. Compact scripts/results are retained under
`docs/performance/typed-internal-20261020`. Copy scripts into the artifact directory
before replay; run from repository root, using the retained approved DiffPlex DLL
and host paths. `benchmark.py <candidate> <baseline>` freshly emits and compiles
before validation/timing. Choose unused candidate/core-capture names. The special
`final` name also requires byte equality with the earlier `borrows.cpp`.
`acceptance.py` validates complete output on constrained threads and reruns .NET.
These scripts assume the documented local retained input artifacts.

Review covered the structural/transitive effect proof, fault replay, alias and
root lifetimes, signed arithmetic, block copies, native stack/call-depth limits,
module declarations and exact measurement identity. No outstanding correctness
finding remains in this bounded slice. No hosted jobs, pushes or services ran.
