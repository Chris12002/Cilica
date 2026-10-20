# DiffPlex performance targets

Recorded 2026-10-20. Approved for implementation, in the order below.
Baseline: `142aecd` compiler/core, with subsequent diagnostic evidence committed
at `7cb76aa` and `b5ff3a5`. Work continues in `codex/profile-v107`.

1. **Direct substring construction — complete (v116).** Preserve managed validation and
   identity shortcuts; allocate a final string and copy source UTF-16 directly,
   eliminating the temporary Int32 vector and per-character managed loop.
   Accepted: line diff 54.52 → 39.50 µs (27.5% less time); character unchanged.
   See [measurements](substring-20261020.md) and ADR 0224.
2. **Compact dictionary entries — investigated, deferred behind array lowering.** Replace separately allocated entry
   objects only after preserving mutable-key callbacks, reentrancy, lookup return
   semantics, resizing, removal/free slots and precise reference tracing.
   Inline entries regressed character diff about 5× through generic borrow
   machinery. Compact columns preserved fast lookup but produced only a small
   preliminary timing change and broke existing UInt64-key support.
   Neither candidate is accepted; see [the retained experiments](dictionary-compact-20261020.md)
   and ADR 0225. Preserve the captured-storage callback probes for a later attempt.
3. **Primitive-array loops and native locals — next.** Extend structural
   execution proofs to remove generic location/frame machinery in diff loops.
   Keep bounds, exception order, aliasing and safepoint lifetime guarantees.
   Include scalarized inline-array reads in the investigation, and inventory
   generic array element support before changing collection representations.
4. **Character-string construction loops — queued.** Reduce call/frame/store
   overhead around fresh one-character strings. Preserve NUL, isolated surrogates,
   independent identity, allocation faults and roots across collection.

The actual .NET machine code and Unity output motivating these targets are in
[the .NET investigation](dotnet-speed-20261020.md); section timings and timer
controls are in [the three-backend comparison](section-timing-20261020.md).

Each implementation slice requires fresh compiler emission, full-output
comparisons, focused semantic/GC/shared-module checks, separate sanitizer and
release runs, comparable before/after timings, code review and a coherent commit.
Instrumented timings rank work; uninstrumented whole-workload timings accept it.

## Opportunities beyond the comparator implementations

Treat these as hypotheses, not novelty or performance claims:

- Closed-world lifetime proofs can retain roots once across a helper chain,
  eliminating repeated frame setup without weakening public boundaries.
- Exact type and storage knowledge can specialize collection representations
  and remove generic dispatch or descriptor work.
- Proven producer/consumer relationships could combine splitting, hashing and
  copying, avoiding duplicate scans while preserving observable allocation,
  exception and callback order.
- Bounded execution may support cheaper allocation/reclamation policies for
  demonstrably nonescaping temporaries.

Prefer improvements available to every qualifying method. Do not hard-code
DiffPlex method names, inputs or expected outputs. A hypothesis advances only
after proof of correctness and an isolated measured gain; beating .NET remains
an objective, not an established result.
