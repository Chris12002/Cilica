# Native scalar locals: v116 to v117

2026-10-20, on `codex/runtime-performance`. Implementation:
[ADR 0226](../decisions/0226-native-scalar-locals.md). Sized beforehand by the
[retained-code experiment](scalar-locals-sizing-20261020.md).

## Change

In optimized direct-control-flow bodies (ADR 0199), Int32/Int64 parameters,
locals, block inputs and IR values become native C++ locals instead of arena
frame slots. Scalars targeted by `AddressParameter`/`AddressLocal` keep leading
frame slots, so managed locations, local-output/local-character carriers and
borrow registration still reach them by index.

Step text uses `CILICA_I(n)`/`CILICA_W(n)`. Step functions map these to frame
slots; the direct rendering maps them to zero-initialized locals declared before
the first branch. `CILICA_NATIVE_SCALARS` defaults to 1 only for optimized direct
builds. Setting it to 0 restores frame-resident scalars for diagnosis; setting it
to 1 without direct flow is a compile error. Frame structs are emitted with their
method once its rendering is known, and hold only frame scalars under native
scalars. References, roots, borrows, arena/depth accounting, the dispatcher, typed
kernels and public boundaries are unchanged.

## Results

Fresh v117 emission (C++ SHA-256 `d82966c8…`, binary `df926711…`) reproduces all
523 character and 2,061 line .NET observations. Timing is one interleaved batch
of v116 (retained accepted executable `0b73e178…`), v117, ordinary .NET 10.0.11
(tiering and ReadyToRun disabled) and the retained Unity 6000.3.2f1 IL2CPP
player. Each backend runs three fresh processes of seven samples with rotating
and reversing order; .NET and Unity verify their outputs first. Medians are in
microseconds per diff:

| Workload | v116 | v117 | .NET | Unity IL2CPP |
|---|---:|---:|---:|---:|
| Character | 22.481 | **20.829** | 5.393 | 26.043 |
| Line | 36.336 | **32.880** | 6.892 | 25.378 |

v117 takes **7.35% less time on characters** and **9.51% less on lines** than
v116. Every v117 process median is below every v116 process median: characters
20.77–20.88 versus 22.29–22.61, lines 32.85–33.05 versus 36.00–36.46. Characters
are now 3.86× .NET and 20.0% faster than this Unity player. Lines are 4.77× .NET
and still 1.30× slower than Unity.

Host load averaged about 11 from unrelated services, and no builds, tests or
profiling of ours overlapped timing. These are local process batches, not
confidence intervals or results for other workloads/platforms.

REA (`morluto/rea` `ee12d517a2`) with Ghidra 12.1.4, ARM64 machine code:

| Method | Instructions | Loads/stores | Basic blocks | Calls |
|---|---:|---:|---:|---:|
| CalculateEditLength | 1,880 → 1,252 | 892 → 440 | 441 → 286 | 35 → 34 |
| BuildModificationData (recursive) | 2,184 → 1,897 | 977 → 766 | 571 → 503 | 65 → 65 |
| BuildPieceHashes | 994 → 965 | 385 → 374 | 220 → 211 | 42 → 42 |

BuildPieceHashes changes least: its references and the borrowed TryGetValue
output stay in the frame. Remaining calls are mostly getters and dispatch,
the targets of later slices. IL2CPP's CalculateEditLength has 369 instructions
and 73 loads/stores, and NativeAOT's has 275 and 54.

`-fstack-usage` frames: CalculateEditLength 112 → 192 bytes, BuildPieceHashes
240 → 272, LineChunker.Chunk 48 → 64. Recursive BuildModificationData stays 304,
and the largest frame stays 432 bytes. Arena frames shrink by up to 1,148 bytes
per call.

## Validation

- Seven new NativeScalarLocalsTests pass. The probe covers long scalar chains,
  Int64 loops, borrowed `ref`/`out` neighbours, recursion, scalars live across
  allocation and forced collection, and writes visible after a caught fault.
  It runs at O0 (default dispatcher) and at O2 with the dispatcher, frame-resident
  direct scalars and native scalars, with diagnostics on and off. Each run uses
  ASan/UBSan, `-Werror`, a 2,048-byte frame warning, three forced-GC repeats,
  exact arena peak and short-arena failure. Structural checks confirm framed
  borrowed slots, native declarations and per-rendering frame sizes. A
  one-method-per-unit shared module compiles, links and runs.
- All 217 `Native*` tests pass, the seven new ones included (native-tests.log).
- The full suite ran 6,126 tests: 6,058 passed and 68 failed under a load average
  of 9–15. The 41 failing methods (214 cases) were rerun on a clean `81c563a`
  worktree and then on v117. The 43 non-timeout failures are identical on the
  baseline: stale diagnostic-text expectations and the existing
  PolymorphicKeyText quota assertion. Every remaining failure was a 30-second
  timeout in the native build step, on both sides for different groups (23
  baseline, 17 v117). Serially, all 150 cases of those timeout-prone methods
  pass on v117. v117 introduces no new failure; see test-results.json and
  timeout-tests-serial.log.
- Forcing direct flow at O0 under ASan already exceeds the harness's 2,048-byte
  frame warning with frame-resident scalars. It is not a default configuration and
  is not covered here; O0 defaults to the dispatcher.

## Evidence

Scripts, raw timings, logs, Ghidra metrics, comparison summary and provenance
are in [scalar-locals-20261020/](scalar-locals-20261020/). Emitted C++, the
binary, stack usage and captured core are in `artifacts/scalar-locals-20261020/`.
