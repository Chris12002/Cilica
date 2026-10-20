# Scalar native locals: sizing experiment

2026-10-20, on `codex/runtime-performance` at the v116 compiler. Diagnostic
experiment on retained generated C++ (step 5 of the library optimization
workflow). **No compiler, runtime or library change is made or claimed.**
This sizes the opportunity for a production slice; it is not acceptance evidence.

## Hypothesis

In optimized direct-control-flow bodies (ADR 0199), every Int32/Int64 IL value and
local is stored in an arena frame slot (`f->integers[n]`). Integer slots are never
GC roots, and methods with local exception regions keep the dispatcher. Clang must
keep these memory stores, and the `uint32_t` slots may alias `context.pending`.
Values therefore cannot stay in registers.

The REA/Ghidra comparison of `Differ.CalculateEditLength` (token `0600000e`) found
892 loads/stores among 1,880 ARM64 instructions in Cilica v116. Unity IL2CPP has
73 of 369 and NativeAOT 54 of 275. The method uses 287 integer slots and borrows none.

## Method

[transform.py](scalar-locals-sizing-20261020/transform.py) rewrites the accepted
v116 benchmark C++ (`substring.cpp`, SHA-256 `2d5bb7c0…`) without regenerating it:

- Only `#if CILICA_DIRECT_CONTROL_FLOW` method bodies change; the O0 dispatcher
  and all step functions are untouched.
- Each `f->integers[n]` / `f->wide_integers[n]` becomes a zero-initialized
  function-scope local declared before the first `goto`.
- Slots a managed location can reach stay in the frame. These are slots used by
  `frame_location` with an integer origin, address-taken slots, and slots passed
  to the local-output or local-character helpers. Six methods keep 1–4 such slots.
- Methods referencing a whole scalar array are skipped (none occurred).
- **Variant A** keeps the frame structs. **Variant B** also removes the scalar
  arrays from frame structs whose methods have no pinned slot and no
  `BorrowScope`. Members are renamed so any remaining live use fails to compile.

The transform converted 4,050 slots in 118 of 144 direct bodies; the other 26
have no scalar slots. All six measured DiffPlex methods convert.

## Results

All three binaries reproduce every retained .NET observation: 523 character
values and 2,061 line values. Medians are in microseconds per diff, from three
fresh processes per variant, seven samples each, 1,024 diffs per sample. Order
rotates and reverses, and every sample's checksum validates.

| Batch | Workload | v116 baseline | Variant A | Variant B |
|---|---|---:|---:|---:|
| 1 | Character | 22.562 | 21.181 (−6.12%) | 20.895 (−7.39%) |
| 1 | Line | 36.168 | 33.988 (−6.03%) | 34.068 (−5.81%) |
| 3 | Character | 22.413 | 21.145 (−5.66%) | 20.831 (−7.06%) |
| 3 | Line | 36.605 | 33.301 (−9.03%) | 33.673 (−8.01%) |

Batch 2 ran concurrently with Ghidra analysis, so it is indicative only. It gave
−4.81%/−6.49% on characters and −8.50%/−7.41% on lines. In batches 1 and 3,
every variant process median is below every baseline process median. Host load
averaged about 9 from unrelated services, and these are local process batches,
not confidence intervals. .NET and Unity were not retimed. The v116 comparison
remains 5.471 µs (.NET) and 25.670 µs (Unity) for characters.

REA 6.7.0 (`ee12d517a2`, source build) with Ghidra 12.1.4, `CalculateEditLength`:

| Metric | Baseline | Variant B |
|---|---:|---:|
| Instructions | 1,880 | 1,252 |
| Basic blocks | 441 | 286 |
| Loads/stores | 892 | 440 |
| Calls | 35 | 34 |

The remaining calls are mostly seven single-field getters. Both IL2CPP and
NativeAOT inline them; that is a separate slice.

Native frames (`-fstack-usage`) grow modestly: `CalculateEditLength` 112 → 176
(A) / 192 (B) bytes, `BuildPieceHashes` 240 → 272, and `CharacterChunker.Chunk`
208 → 224. The recursive `BuildModificationData` stays at 304 bytes. The
largest frame in the binary is 432 bytes in every variant. Variant B also
removes up to 1,148 bytes of zero-initialized arena frame per call.

## Decision and production slice

Proceed to an ADR and production implementation. The two variants are close,
and B is consistently best on characters. Production should emit locals and omit
unused scalar frame storage. Requirements beyond this experiment:

- Decide eligibility from IR, not text. A local or argument whose address can
  reach a managed location stays in the frame, as do slots registered with
  `BorrowScope` and every typed-kernel, local-output and local-character carrier.
- Keep references in `f->roots`; GC precision is unchanged.
- Keep the dispatcher form (O0, local exception regions, more than 512 steps)
  unchanged.
- Confirm that no runtime, diagnostic or exception path reads scalar frame slots
  by index.
- Re-check the ADR 0202 frame threshold and the 512–576 KiB constrained-stack
  run, including recursive and shared-module call chains.
- Validate with reduced probes for long integer chains, Int64 values,
  address-taken neighbours, borrowed outputs and recursion. Run O0/O2 .NET
  differential checks, forced GC, ASan/UBSan with diagnostics on and off, and
  shared modules. Then regenerate, time paired against v116, .NET and Unity, and
  repeat the REA comparison.

## Evidence

Scripts, transform report, raw timings for batches 1 and 3, summaries and
provenance are in [scalar-locals-sizing-20261020/](scalar-locals-sizing-20261020/).
Provenance records hashes, the build command, the oracle hashes, frame sizes,
Ghidra metrics and tool versions. Large C++, binaries, stack-usage files and
Ghidra evidence are in `artifacts/scalar-locals-sizing-20261020/`.
