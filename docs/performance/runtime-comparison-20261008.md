# NativeAOT / IL2CPP: which root-tracking ideas transfer?

**The useful lesson is to separate reference-location knowledge from ownership
validation.** NativeAOT's inspected fast allocation path relies on runtime/compiler
invariants; Cilica additionally validates references against an instance's current
allocation inventory. Copying the fast instructions without preserving that contract
would recreate the rejected index's defect.

This is a read-only comparison and proposal, not an adoption decision. No external
source or new kernels were executed; Unity was not installed and no licenses were
accepted. NativeAOT source was read at immutable dotnet/runtime commit
`79d0c463f1b55624c874a11585f7e47731e8d675`, the pin labelled v10.0.11 by Cilica's
library provenance—not a claim about latest main or the installed .NET runtime.
[Source hashes](runtime-comparison-20261008/nativeaot-source-receipt.json) record
seven inspected files. Unity evidence is versioned 6000.0 documentation retrieved
2026-10-08 plus an explicitly historical example; no current Unity binary or
emitted project was inspected.

## What the inspected implementations establish

| Evidence | Mechanism and assumption | Consequence for Cilica |
| --- | --- | --- |
| NativeAOT ARM64 [`RhpNewFast`](https://github.com/dotnet/runtime/blob/79d0c463f1b55624c874a11585f7e47731e8d675/src/coreclr/runtime/arm64/AllocFast.S#L7) | Reads type size, checks remaining allocation-context capacity, installs the type pointer and advances the allocation pointer. The ordinary successful path contains no whole-stack root scan. Slow allocation records a transition frame. | The fast path presumes a valid type and runtime-managed allocation context. It does not establish arbitrary pointer ownership or Cilica's per-instance quota/fault contract. Bump allocation alone would leave our repeated validation cost intact. |
| NativeAOT Unix [`EnumGcRefs`](https://github.com/dotnet/runtime/blob/79d0c463f1b55624c874a11585f7e47731e8d675/src/coreclr/nativeaot/Runtime/unix/UnixNativeCodeManager.cpp#L207) and [`Thread::GcScanRootsWorker`](https://github.com/dotnet/runtime/blob/79d0c463f1b55624c874a11585f7e47731e8d675/src/coreclr/nativeaot/Runtime/thread.cpp#L427) | The code manager decodes GC lifetimes at a code offset and enumerates live locations using a register display. Thread scanning walks frames. Conservative reporting also exists, including exceptional stack ranges; this is not an “always precise everywhere” claim. | Compiler-known root locations/lifetimes are a useful direction. Cilica can use validated IR and its existing shadow roots without adopting platform unwinding/register maps. Dropping published roots changes retention and possibly bounded-allocation failure behavior, so it needs an explicit contract decision. |
| NativeAOT [`GcEnum.cpp`](https://github.com/dotnet/runtime/blob/79d0c463f1b55624c874a11585f7e47731e8d675/src/coreclr/nativeaot/Runtime/GcEnum.cpp#L62), [`ObjectLayout.h`](https://github.com/dotnet/runtime/blob/79d0c463f1b55624c874a11585f7e47731e8d675/src/coreclr/nativeaot/Runtime/ObjectLayout.h#L40) | Precise object references are passed to the GC callback; object accessors read their type pointer. The separate conservative path filters by heap address bounds and reports pinned/interior candidates. Its comment explicitly distinguishes range filtering from proof of a real object. | Range membership, a plausible header and a type pointer are insufficient authorization for Cilica. Preserve foreign/interior/detached-object and stale-handle rejection before dereferencing unknown inputs. A raw address reused by a new live allocation has no generation guarantee. NativeAOT's inspected paths do not provide an equivalent per-instance corruption validator. |
| Unity 6.0 [GC modes](https://docs.unity3d.com/6000.0/Documentation/Manual/performance-incremental-garbage-collection.html) | Documents Boehm collection, default incremental scheduling, reference-write barriers and non-compacting behavior. Incremental work changes pause distribution; mutation can cause rescanning or fallback. | Useful prompts for a reference-write kernel and GC-boundary measurements. These docs do not verify current root-map or heap-membership implementation details, nor equivalent isolation/bounded work. Incremental GC is not the fix for a baseline where collection was only 0.114% of instrumented elapsed time. |
| Unity's [2015 root integration article](https://unity.com/blog/engine-platform/il2cpp-internals-garbage-collector-integration) | The Unity 5.1.0p1 example registers thread stacks, separately allocates static-root storage and uses handles for native-held objects. Its private APIs are explicitly unstable. | The separation of roots, statics and native retention is instructive and already has Cilica counterparts. Those particular generated names/APIs are historical evidence only; do not call them or treat their code as current Unity output. |

**Inference:** these fast paths amortize GC bookkeeping under compiler/runtime
invariants rather than proving membership in an externally mutable allocation list
for every root at every allocation. They are not evidence that Cilica can safely
remove checks. Neither the inspected NativeAOT paths nor the Unity docs establish
Cilica's exact inventory-corruption, bounded native-frame, retained-handle and
instance-isolation contract.

## Ranked next investigations

1. **Keep the existing ownership contract; compare bounded same-call membership
   work.** Use the reviewed lazy-prefix experiment as an experimental control,
   compare scratch caps and alias-query deduplication, and measure root queries,
   nodes visited and actual capacity separately from clean time. Reject stale
   authorization, unreadable-tail lookahead and unbounded fallback. The tradeoff
   is explicit scratch cost and lookup/zeroing work; no default budget is adopted.
   This is the smallest direction supported by the measured bottleneck.
2. **Reduce redundant root publication with compiler evidence.** First measure
   aliases and slots already provably null at existing safe points. Preserve
   published-slot diagnostics; do not simply ignore a non-null “dead” slot.
   Full liveness pruning is a later design decision because earlier reclamation
   can change retention and quota failures. NativeAOT's lifetime maps motivate the
   analysis, but Cilica's detached IR validation, exception/finally paths, inline
   reference leaves and borrows must independently prove it. Tradeoff: more
   compiler proof and metadata complexity for fewer root checks.
3. **Profile ordinary reference operations on the revised implementation.** The
   original baseline had 22,750 ordinary ownership queries per diff. Use a
   no-allocation read kernel and reference-write kernel to separate lookup,
   type checks, dispatch and copy costs before proposing another cache. Any
   reuse must expire at callbacks, mutation, collection or other invalidation
   boundaries, and must preserve current readable-corruption diagnostics.
   A persistent index requires an ownership-authority design; the old one stays
   rejected. The old index's residual percentages are not the prefix's profile.
4. **Defer allocator/collector replacement.** NativeAOT's allocation-context path
   is worth an eventual fixed-size-allocation comparison, not a runtime rewrite
   now. A Cilica arena would need exact object-start metadata, accounting for
   reserved capacity/fragmentation, fail/retry semantics, reclamation and reuse
   tests. Address bounds alone are insufficient. Unity-style incremental work or
   conservative retention must not replace deterministic instance accounting.

## Proposed owned comparison inputs

[`Kernels.cs`](../../tools/Performance/runtime-comparison/Kernels.cs) contains
independent, unexecuted C# reductions with no Unity dependency. Their source shapes
are hypotheses, not claims about emitted root or allocation counts. The companion
[protocol](../../tools/Performance/runtime-comparison/README.md) defines setup,
checksums, input bounds and inspection points. Keep native boundary corruption,
cross-instance and stale-handle cases in the existing owned native suite: portable
safe C# cannot express those invalid pointers, and ordinary .NET/Unity execution
cannot serve as their oracle.

## Measurement limits that remain binding

The reviewed **lazy-prefix** result is **261.225 → 38.041 ms/diff (6.867×)** on one
pinned 128-character DiffPlex workload. The **507× persistent-index result remains
rejected** for ownership/stack/resource defects. Neither is a NativeAOT or Unity
comparison; no speedup from the proposals above is promised.

Historical `wait4` RSS covers launch lifetime and can inherit the Python parent's
pre-exec high-water floor. Use the [RSS correction](lazy-prefix-20261008.md), separate
post-exec memory diagnostics, charged heap/scratch counters and clean timings.
Keep startup, steady compute, end-to-end, allocation volume and retained memory
separate. A future NativeAOT/Unity build/run needs a separately approved exact
setup and execution plan; this read-only work grants none and changes no production
memory policy. Source/architecture review only: no new conformance claim.
