# Ownership index: adoption review and residual fingerprint

**Recommendation: do not adopt the experimental patch as written.** The speed
finding is strong, but independent review found a semantic mismatch and a native
stack-budget violation, both reproduced locally in the approved offline guest.
No production source, ABI, profile or limit was changed.

The compatibility lane (`Expand Cilica compatibility`, thread
`01a1197b-2170-740a-9bfb-04c4a046c0d7`) independently reviewed the exact transform,
allocation/collection/tracing implementation and existing corruption/frame tests.
`Review milestone 6` was also asked for architectural review; no affirmative
adoption decision was received. The following findings are from independent review
plus an executed reduced test, not a speculative objection to optimization.

## Demonstrated adoption blockers

1. **The list and index disagree about membership after a readable topology mutation.**
   The reducer roots an existing object, detaches it from `head/next`, adjusts count
   and charged bytes to the remaining inventory, and invokes `mark_reachable`.
   Baseline: `MarkStatus::invalid`, `owns=false`, orphan unmarked. Experiment:
   `MarkStatus::complete`, `owns=true`, orphan marked. Its mark is not reset by the
   inventory cleanup because the orphan is outside the list. The test restores the
   original inventory before teardown and passes ASan/UBSan; it does not free or
   dereference arbitrary host memory. This directly violates the baseline's
   inventory-before-marking contract. No GC optimization may silently relax it.

2. **Memory and native-frame budgets change.** `sizeof(Instance)` increases from
   144 to 65,688 bytes on this ARM64 target: 65,536 pointer-table bytes plus flag and
   padding, **65,544 bytes extra**, even for an empty instance. Both versions still
   report zero initial `heap.bytes` and the same 1,048,576-byte managed limit.
   Clang reports a **66,304-byte frame** in the generated packed native entry and
   66,432 bytes in the boundary-test main. Baseline compilation has no such warning.
   Existing `NativeTracingTests` require static native frames at most 2,048 bytes
   with `-Werror -Wframe-larger-than=2048`; those requirements would be broken.

3. **Hashing is ARM64-specific as written.** The 64-bit constant and shift by 47
   need width-correct handling or explicit target rejection for 32-bit pointers.
   No 32-bit execution/compatibility claim is made. This is a source-review finding,
   separate from the two executed failures above.

The normal supported mutation path is promising: all three allocation publication
sites insert only after successful allocation/buffer initialization; sweep rebuilds
before returning; no managed callback intervenes during reclamation. Exact pointer
equality prevents dereferencing unknown inputs during lookup, and a saturated table
falls back to the old list search. The 523-value transcript passed normal execution
and forced-GC ASan/UBSan, including teardown. These passes do not erase the demonstrated
corruption mismatch or establish correctness for all configured limits.

## Required design and regression gates

The smallest next production-design proposal is a **bounded, stack-safe exact
membership acceleration structure with an explicit memory charge/reserve policy
and one validated ownership contract**. Preserve inventory corruption detection at
every required native/diagnostic/allocation boundary. Simply trusting a mutation
counter, rebuilding from an unvalidated list, or reading `value->owner` on an unknown
pointer does not solve the reviewed contract. Moving the table to dynamically
allocated storage fixes stack size only; it introduces allocation-failure and
accounting decisions and does not fix the dual-authority mismatch.

Before implementation, agree the invariant and budget policy with the feature lane
and present the resulting architecture design for feedback. Then validate:

- Exact list/index disagreement, detached roots, foreign insertion, duplicate/cyclic
  links and bad count/bytes. Invalid/limit/busy exits must be atomic: no new marks,
  frees, changed counters or stale index; restore and retry must succeed.
- Forced collision clusters and wraparound, 8,192 occupied buckets plus an 8,193rd
  object under deliberately larger diagnostic bounds, fallback and recovery below
  capacity. The executed micro-reducer covered only inventories through 4,096.
- Sweep all/none/alternating survivors, rooted cycles/aliases, all three allocation
  families, failed buffers/allocations and exhausted quotas. Check reclaimed pointer
  rejection before reuse, deterministic reuse, and stale retained-handle tokens.
  Raw addresses have no generation guarantee in the baseline; do not invent one.
- Two-instance isolation for roots, fields, array elements, borrows, handles,
  casts and calls; null, interior, foreign and address-1 inputs must reject safely.
- Empty/multiple-instance overhead, exact heap limits, nothrow creation failure,
  recovery/disposal, and the existing 2 KiB O0/O2 native frame checks.
- Existing `NativeTracingTests`, `NativeObjectExecutionTests`,
  `NativeCollectionTests`, `NativeFrameBudgetTests`, `NativeExecutionFrameTests`,
  `NativeExceptionRuntimeTests`, runtime-type/cache/handle tests and the pinned
  DiffPlex transcripts at O0/O2 with forced GC and ASan/UBSan.

The large suite above is a **required future gate, not a claimed pass**. Running it
against a known-invalid integration would not justify adoption. Production-file
ownership remains with feature work; no shared optimization has been implemented.

## Largest residual costs on the indexed experiment

A separate offline diagnostic consumes the exact retained generated C++ SHA256
`51d73790b77e9bd83d330785bcc12f975153840a9e8d37aec9462719083d26d7` and the same index
transform. It does not rebuild or broaden the candidate. One variant adds counters
and coarse scope clocks; another samples Linux ARM64 instruction PCs with SIGPROF
during 16,384 unchanged diffs. Sampling records **8,304 PCs** and checksum
100,696,064. Existing image tools Clang 18.1.3 and GNU Binutils 2.42 symbolize PCs;
no profiler package is installed. Sampling uses O2, debug locations, frame pointers
and a non-PIE executable. It is **diagnostic CPU attribution, not a new clean timing**;
other feature work was allowed during this run.

| Rank | Measured region | Share of sampled PCs | Interpretation |
| --- | --- | ---: | --- |
| 1 | Generated statement bodies and method drivers | 33.07% + 13.58% = **46.65%** | Includes useful managed operations, checks, frame setup and trampoline dispatch; not all is removable overhead. |
| 2 | Pre-allocation root validation | **16.20%** | Coarse counters independently put inclusive root validation at 17.73% of instrumented time. |
| 3 | Managed locations and borrow validation | **11.08%** | Includes active-frame/list searches, borrow registration/retirement and storage validation. |
| 4 | Descriptor/type acceptance checks | **9.79%** | One 44-entry exact-membership table accounts for 680 samples, 8.19% of all PCs. |
| — | External libc/libstdc++ code | 7.73% | Some malloc/free symbols resolve; many PCs are internal stripped symbols. No full allocation attribution claim. |
| — | Collector tracing / allocator helpers / other ownership / other native | 2.10% / 1.57% / 0.78% / 4.11% | Leaf/inlined-PC categories, not inclusive call-stack costs. |

Across 21 instrumented batches of 16 diffs, counters record per diff:

- **110,579.75 statement-table dispatches** and **5,217.0625 frame reservations**;
  the fractional parts reflect one outer batch frame/loop, not fractional objects.
- **2,534,927.5 bytes of cumulative execution-frame reservations/default-initialized
  layouts**. This is repeated use of a bounded arena, not managed allocation or
  actual measured memory-write traffic; the original peak frame arena is 11,712 bytes.
- **122,000 active root slots inspected**, including **54,369 non-null slots**,
  at 824 validation calls. Null slots still incur loop and check overhead.
- Allocator-inclusive scope time is **36.33%**, containing root validation (17.73%)
  and collection (6.15%). These nested scopes must not be added as disjoint costs.
  Clock/counter overhead perturbs this binary; sampled PCs provide a separate check.

The next ranked investigation is **statement dispatch and frame setup**, supported
by 46.65% sampled execution region and over 110,000 dispatches per 128-character
diff. The emitter deliberately uses noinline step functions and a function-pointer
table to meet O0 sanitizer/native stack limits. A narrow straight-line reduction
should isolate trampoline/frame overhead from useful work before proposing changes;
any experiment must retain exception order, GC root publication and the 2 KiB frame
contract. **No speedup or broad emitter rewrite is proposed as established.**

The smaller, more isolated descriptor-membership hotspot is also concrete:
`known_type` routes through the 44-entry `...02000066_accepts` loop. A future
immutable exact-descriptor lookup experiment could test that cost independently.
It must preserve rejection of copied/unknown descriptors. Neither dispatch nor
descriptor lookup was optimized in this task. Boxing/copy and individual
string/collection algorithms remain unattributed within generated execution.

Periodic sampling may alias with repeated work, and leaf PCs do not reconstruct
call stacks. Region shares are not confidence intervals or additive with scoped
wall-time measurements. Exact counters and source structure support the ranking;
clean before/after evidence is still required for any next proposed change.

Raw diagnostic commands, transformed-source/binary hashes, source locations,
samples, counters, boundary outputs and stack warnings are retained in
[residual evidence](residual-20261008/analysis.json). The offline container ran
81.4 seconds with the same resource/isolation limits and was removed afterwards.
