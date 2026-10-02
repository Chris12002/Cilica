# ADR 0028: Packed object boundaries and arena lifetime diagnostics

Status: accepted for the bounded object backend. Recorded 2026-10-02.

Follow-up: [ADR 0029](0029-measured-helper-batching.md) records local dispatch
microbenchmarks and rejects a larger ordinary-statement batch. The unmeasured
performance limitation below describes this checkpoint.

## Context

Review after ADR 0027 found that the experimental export still placed one native
parameter per managed parameter on the host stack. A valid 4,096-parameter export
used 49,392 native stack bytes at O0, before its execution budget could protect it.
Internal calls already used budgeted argument packets.

The arena retained its backing allocation until context teardown. ASan therefore
could not detect access to a released reservation. This was a diagnostic gap,
not evidence of an existing dangling reference. Finally, per-function stack
reports did not exercise the full native chain on a constrained host stack.

## Decision

1. Publish `object-frames-native-v2` with a packed Int32 boundary. Four output
   pointers are followed by `const std::int32_t* parameters, std::size_t count`.
   Require the exact compiled arity and a non-null input pointer for nonzero
   counts. Required null pointers and count mismatches return status 3 without
   touching outputs, inputs or the arena. Native callers remain responsible for
   readable inputs and valid, disjoint outputs. Copy inputs with a loop into the
   existing budgeted entry packet. Internal call semantics, fault precedence and
   default budgets are unchanged.

2. Name the export `_objects_packed_try`, so an old individual-parameter caller
   cannot silently link to an incompatible signature. The primitive emitter's
   ABI is unchanged. This remains an experimental compiler API, not a persistent
   runtime or stable embedding interface.

3. Detect ASan instrumentation during native compilation. Poison the initial
   unused arena, unpoison typed storage before construction and poison it after
   destruction. Existing scope order unlinks roots first. All reservations start
   at an alignment of at least 8 bytes, keeping adjacent live reservations in
   distinct shadow granules on the tested target. Apply the same alignment in
   ordinary builds, so diagnostic flags do not change execution budget accounting.
   Failed reservations leave the cursor and poison state unchanged. Reuse starts
   a fresh typed lifetime and restores accessibility; stale pointers to a reused
   live address remain outside this diagnostic's detection capability.

4. Execute the wide finite/mutual-recursion regression on a pthread requesting
   256 KiB, in both ordinary and ASan/UBSan O0/O2 builds. On macOS, inspect the
   actual size and enforce a 320 KiB ceiling. A minimal independent probe showed
   that macOS supplies 268 KiB with and without sanitizers. Keep the 2 KiB native
   frame warning and inspect ordinary static stack reports, including the harness.
   This validates the authored chain with depth 64, runtime allocation, faults
   and cleanup on this target; arbitrary host callbacks and other targets still
   need separate validation.

## Validation and limitations

Packed-boundary regressions cover arities 0, 1, 1,024 and 4,096; exact and invalid
counts, null inputs, unchanged failure outputs and ordinary results. Apple Clang
17 macOS ARM64 reports maximum export stack sizes of 288 bytes at O0 and 224 at
O2. The recursion translation unit including its harness has per-function maxima
of 1,520 and 768 bytes. Those measurements cover different translation units and
are not interchangeable with ADR 0027's original method-only measurements.

Lifetime tests check nested byte-sized reservations, untouched live neighbors,
poisoned unused storage, zero-initialized reuse and context cleanup. A separate
test-owned process deliberately reads a released slot and must report ASan
`use-after-poison` at both optimization levels. Existing reference-root and
fault-cleanup probes run with the new poisoning enabled.

Final review checked packed ABI callers, validation before side effects,
subtraction-based reservation bounds, shadow-granule separation, root unlinking
before poisoning and reuse after faults. The build has zero warnings/errors;
all 420 targeted Native/Object/Cpp/Int32 tests pass in 26.9 seconds. No additional
defect was identified in this slice. Local main and cached origin/main remain
ancestors of the working branch; no push or hosted job was started.

No collector, broader platform support or throughput improvement is claimed.
The conservative helper dispatch remains unbenchmarked; optimizing it requires
new correctness and native-stack measurements. No hosted work is required.
