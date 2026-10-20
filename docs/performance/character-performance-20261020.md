# DiffPlex: single-allocation strings and rooted character helpers

2026-10-20. Apple M4 Pro, macOS ARM64, Apple Clang 17, .NET 10.0.11.

Implemented the two character optimizations selected by the
[v99 investigation](v99-profile-20261020.md). These measurements use fresh compiler
output, not the earlier generated-code diagnostic experiments.

## Results

Three alternating processes per version, seven samples of 256 diffs per process,
at least 300 ms warmup. C++20, -O2 -DNDEBUG, disabled C++ exceptions/RTTI, no
sanitizers during timing. Every timed checksum is 6146 per diff. Builds, tests
and sampling did not run concurrently with measurements; all samples are retained.

| Version | Median us/diff | Range across 21 samples |
| --- | ---: | ---: |
| v99 baseline | 73.825 | 71.531–76.920 |
| v100: inline one-character strings | 66.051 | 64.130–70.594 |
| v101: also rooted direct wrappers | 61.511 | 60.257–74.184 |
| Ordinary .NET, non-tiered | 5.491 | 5.299–5.640 |

Both changes reduce native execution time by **16.68%** (1.20x throughput).
The second slice reduces time by **6.87%** relative to the first. With the fresh
.NET series, the gap decreases from **13.44x to 11.20x**. The .NET host uses
DOTNET_TieredCompilation=0 and DOTNET_ReadyToRun=0, three processes, seven samples
of 16,384 diffs each. No new NativeAOT or Unity timing is claimed.

The first isolated slice measured 72.635 → 65.023 us/diff (10.48% less time).
That earlier pair and the final three-version series are distinct measurements.

## Implementation and correctness

- [ADR 0207](../decisions/0207-inline-character-strings.md), commit `2a8407c`:
  embed one UTF-16 code unit in StringObject. Charge its storage once and skip
  separate buffer allocation/deletion; preserve fresh identity.
- [ADR 0208](../decisions/0208-rooted-character-helpers.md): recognize two exact
  operation shapes, retain real GC frames and the borrowed owner, avoid general
  execution-arena frames on valid nested calls. No replay after allocation.

There are 58 distinct passing focused checks across the two slices (16 then 44,
with two inline-storage tests shared). New storage tests exhaust 65,536 UTF-16
units, count allocations, inject header/buffer failures and exercise collection.
Helper tests check expired/foreign/wrong-width borrows, cold entry, allocation
failure origin, recovery, boundary depth and separate translation units.
The final three helper checks were repeated after adding depth-64 success.
Existing character/.NET tests cover payloads, aliases, identity and fault order;
dictionary and typed-borrow regressions also pass.

The first wrapper regression run found seven stale assertions expecting a
writable Char receiver. The core declaration was already readonly before this
work. Correcting that expectation restores the intended mutation checks. The
initial failing log and final passing log are retained.

All **523 DiffPlex observations** match the retained .NET oracle in release and
forced-GC O1 ASan/UBSan builds. Both execute on a requested 512 KiB pthread stack,
checking actual size within 512–576 KiB. Diagnostic builds enable internal
reference and borrow-lifetime checks. Maximum individual native frames remain
432 bytes release and 320 bytes diagnostic. This establishes this target/workload,
not arbitrary-depth stack safety on other platforms.

## Provenance and comparator evidence

Baseline compiler `12dab27`, implementation `8f951a5`, v99. The inline slice is
`2a8407c`; wrapper emission records that parent plus complete compiler-file hashes.
Subsequent committed regeneration must reproduce the measured C++ byte for byte.

| Artifact | SHA256 |
| --- | --- |
| v99 C++ | `e66a1ab85cc5cbf9da55a81f9f29486c80e33ad786d00da6e882265573cc09bd` |
| v100 C++ | `f5394341015298acd674121ee84f12a32de5bb0c46f6a214b32949dabcc63d9f` |
| v101 C++ | `efa98008bcfd4a89c17622128bef9f5fd7eff10fb5c9e86a440f165b50cf5740` |
| Owned core DLL | `655cc45158b2bd8ad5b06bfecbe8d76f25174b0cf92b86048ea7219bed4d37b1` |
| DiffPlex input DLL | `652ab88f876736bf50d11bdcbadf4ba411a3c4bc91fb6b09ee038f1709ce60f8` |
| DiffPlex input PDB | `4eac2d8ae6c2e7efaade8bca707bcbbe5673dc317cf141176fd4ce7b6ee285b8` |

The input, owned core and their Roslyn IL are unchanged. CharacterChunker
`06000021` loads a character address at IL_001a and calls Char.ToString at IL_001c;
the owned instance/static bodies are `060002a2`/`060002a3`, eight/seven IL bytes.
The previous investigation retains the exact IL/options and the Roslyn revision
mismatch disclosure. Its .NET ARM64 code uses FastAllocateString(1) with inline
character storage; Unity 6000.3.2f1 generated wrappers call CreateFromChar, whose
runtime uses one object/data allocation. Those comparator artifacts were inspected
previously; neither compiler source matching nor Unity execution was repeated here.

At the start of this work, ObjectCppEmitter.cs again contained the old v96
overwrite. Its diff was preserved locally and the committed v99 restored before
building the baseline. The writer remains unidentified. Compiler hashes were
checked against emission and committed regeneration; no unrelated process was
stopped. Local main is an ancestor of this branch; no push or hosted job ran.

Compact evidence and replay scripts are in
[character-performance-20261020](character-performance-20261020/). Large generated
sources, binaries, raw runs and rollback patch remain in the ignored
artifacts/character-performance-20261020 directory. Replay scripts are intended
to run there after copying them; input/oracle paths identify retained local assets.
