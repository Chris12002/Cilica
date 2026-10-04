# ADR 0054: Bounded collection and handles checkpoint

Status: Milestone 4 complete for the bounded native profile. Recorded 2026-10-04.

## Supported boundary

Native v14 composes precise frame/literal/handle roots, inherited fields and
reference-vector slots with iterative marking, synchronous non-moving sweep,
recorded allocation accounting and one collection attempt per allocation on
pressure. Diagnostic mode collects before eligible allocation. Invalid or bounded
incomplete tracing frees nothing. Strong retained handles have explicit ownership,
nonreused token generations and bounded native storage. Phase A publishes all
reference values; this is not an optimized liveness collector.

Default limits remain 1 MiB of managed storage, 4,096 allocations, 1 MiB of execution
storage, 64 active frames and 1,024 root slots per frame. Collection defaults to
4,096 inventoried objects and 262,144 root/child edge visits. Handle storage adds at
most 1,024 native slots (16 KiB on the tested target), allocated lazily and outside
the managed byte quota. Native allocation headers and backing extents are trusted
runtime-owned storage. Callers serialize an instance and supply its sole active
context; independent instances do not share managed objects or handles.

This completes the milestone's bounded cycle-reclamation, live-root stress and
private-handle gates. It does not provide a stable object embedding ABI, mobile or
WebAssembly evidence, multithreading within an instance, multi-context root
registration, weak references, finalizers, generational/concurrent collection,
phase B root liveness, general managed exceptions or broader library support.
Named retention paths and per-type allocation statistics remain future diagnostics.
Token values are closure-private and not serialized across compiled libraries.
Actual operating-system memory exhaustion is not deliberately induced by the test
suite; quota failures, cleanup and retry are tested without exhausting the host.

## Integration and review

The inventory example now runs every probe through both the packed boundary and a
private persistent instance collecting before allocation, compares results and
fault origins, then checks post-unwind reclamation. Application rules are unchanged;
the metadata-only emitter remains separate from the authored .NET oracle.

Review of the expanded native corpus found old quota assertions based on cumulative
allocations. They now measure the live set at each allocation point and require
failure below the needed live-object budget or a .NET-equivalent successful result
when collection can make room. Frame-budget, origin and cleanup assertions remain.

Final selected regression run: 196 passed, zero failed/skipped, including O0/O2
ASan/UBSan runtime tests and both examples. The final tracing/collection/handle
harness measures maximum static frames of 768 bytes at O0 and 560 at O2, below the
existing 2 KiB cap. The solution builds with zero warnings/errors. Reviewed
allocation partial-failure cleanup, sweep unlinking/destruction, mark guard cleanup,
rooting across string-buffer reads and calls, fault preservation, handle token
exhaustion and instance teardown. No remaining finding within this scope.

Each implementation slice was reviewed and committed locally. Local main remains
an ancestor; no push, hosted CI, paid service or additional agent ran. Evidence is
macOS ARM64 with the recorded .NET/Apple Clang toolchain, not a portability claim.
