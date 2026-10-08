# Lazy-prefix ownership experiment — 2026-10-08

This separate experiment replaces the rejected persistent index with membership
memoization scoped to one synchronous `valid_roots` call. It is **not a production
runtime change**. The earlier 507-fold experiment and its correctness rejection
remain immutable in [the original report](diffplex-20261008.md) and
[ownership review](ownership-index-review.md). Its speedup cannot be assigned to
this narrower implementation.

## Design and compatibility boundary

The linked allocation inventory remains the only ownership authority. Ordinary
`Instance::owns`, tracing, marking and allocation publication remain unchanged.
A batch records only the prefix it has actually visited. Null and cached queries
read no links. A found node pauses the frontier before reading its next link;
only a later miss advances it. No positive membership result survives the call.
Thus detaching a rooted object between calls cannot leave cached authorization.

A full or unavailable table restarts an allocation-free search from the head.
Brent cycle detection compares pointer values without lookahead dereferences.
Missing roots in readable cyclic inventories now reject instead of reproducing
the baseline's nontermination. This is a deliberate corruption-diagnostic
strengthening, not universal behavioral equivalence. Neither version can safely
search through arbitrary unreadable native links. Both accept a root at `A` when
`A.next == (Object*)1` without following that link; both fail under sanitizers
when a subsequent absent-root search needs that unreadable tail. No general
memory-corruption defense is claimed.

An active guard is installed before scratch allocation. Nested validation uses
an independent allocation-free search and cannot clear, free or zero the outer
batch's table. The implementation assumes the existing single execution thread
per instance and no list mutation during membership queries. The authored
allocator hook runs before traversal; limits are rechecked after that hook.

Scratch storage moves off the native stack. Its explicit experimental budget is
at most **8,192 pointer slots and 65,536 bytes**, separately from the existing
1 MiB charged managed budget. On a 32-bit target the slot cap would permit only
32 KiB; the hash now uses width-safe arithmetic, but 32-bit execution is untested.
Capacity is a power of two within both caps. Growth frees the old buffer first;
allocation failure leaves zero capacity and uses the fallback. A changed/shrunken
limit takes effect at the next root-validation batch. Successful collection,
including zero reclamation, and destruction release the buffer. Empty instances
allocate no scratch.

Counters record allocated capacity, scratch peak and the current/peak sum of
charged managed storage plus scratch. All six managed charge sites update that
sum, including metadata-only runtime type/static/initializer storage. These are
**two-budget accounting**, not total process memory: context execution storage,
other pre-existing native bookkeeping, allocator overhead and instance fields are
not included. Managed capacity is unchanged; total permitted native overhead has
explicitly increased by up to 65,536 dynamic bytes plus 56 fixed Instance bytes
on this target, before allocator overhead. The batch guard uses 32 native-stack bytes. This policy still needs production design approval.

## Correctness evidence

All candidate execution used the same approved offline ARM64 tooling image,
2 CPUs, 8 GiB RAM, 256 processes, no network/host mounts/credentials, 10 GiB
scratch, 1 GiB output and 1,790-second watchdog. The exact original source hash
is `51d73790b77e9bd83d330785bcc12f975153840a9e8d37aec9462719083d26d7`;
compiler/library and candidate pins are unchanged from the original report.

- 204,682 authored assertions pass at each of O0 and O2 with ASan/UBSan.
  They cover collisions and wraparound, 8,192/8,193 entries, every position of
  small cycles, absent/duplicate/null roots, tiny/zero/non-power/overflow caps,
  scratch-allocation failure, nested validation and post-hook cap changes.
- Detached rooted objects reject without marks. Count/byte/owner/cycle inventory
  errors remain atomic; restoration permits retry. Limit/busy collection preserves
  storage; successful and zero-reclaim collection release it. Cross-instance roots
  reject. Post-collection DiffPlex survivors remain eight objects/5,488 charged bytes.
- Forced-GC DiffPlex matches all 523 .NET transcript observations at both O0 and O2,
  with ASan/UBSan and leak detection. Checksum remains 6,146 per diff.
- All 19 existing tracing, object execution, collection, frame-budget, execution
  frame, exception and runtime-type tests pass after applying only this transform
  to a disposable export of compiler commit `3d0bb53`.
- All functions in the benchmark diagnostic translation units meet the 2 KiB
  native-frame gate: maximum 1,952 bytes at O0; revised boundary tests 352 bytes
  and forced-GC benchmark 256 bytes at O2. The test-enabled Instance is 208 bytes
  (includes a Boolean failure injector and callback); the normal Instance is
  200 bytes versus baseline 144 (+56 fixed bytes), and the batch guard is 32 bytes.
  Compile-time size checks record actual target padding.

Two earlier diagnostic builds failed because the combined authored topology test
helper itself exceeded the stack gate (2,176 then 2,144 bytes). Splitting the
independent linear/cyclic cases resolved it. Both failures are retained, not
counted as passing tests. Expected unreadable-tail sanitizer failures are separate
from successful transcript runs, whose stderr is empty.

Raw commands, source/binary hashes, frame receipts, isolation receipts, failed
attempts and exact executed transformations are under
[`lazy-prefix-20261008/`](lazy-prefix-20261008/). Correctness timings are diagnostic
only and must not be compared with clean compute timings.

## Clean measurement

Three fresh processes per variant, alternating order, each with seven 16-operation
samples. The same `steady_clock` nanosecond timer, input and checksum are used.
Warmup is at least 300 ms in batches of eight: baseline used eight operations
(1.43–1.45 s), prefix used sixteen (0.523–0.539 s). Allocation-triggered GC and
1 MiB/4,096-object bounds remain unchanged; heap phases are not identical.
Flags are C++20, O2, NDEBUG, no exceptions/RTTI, no sanitizers or forced GC.
Scope clocks, sampling and assertion injectors are absent from these binaries.

| Measurement | Baseline | Lazy prefix |
| --- | ---: | ---: |
| Steady median per diff | 261.225 ms | 38.041 ms |
| Steady sample range | 213.958–276.630 ms | 35.214–40.990 ms |
| First-call median | 34.995 ms | 11.574 ms |
| Cold command median | 35.828 ms | 12.408 ms |
| Equal-output no-op command median | 1.070 ms | 1.037 ms |

The median ratio is **6.867×**, with paired process-median ratios 6.726–6.907×.
This supersedes any attempt to attribute the rejected persistent index's 507×
improvement to a correctness-preserving revision. It does not generalize to other
workloads. No .NET process was measured in this revision; original .NET figures
remain historical. Input setup and serialization are outside steady compute;
command times include launch and output and are not pure startup.

Compatibility explicitly released and held the window; the other feature lane
was notified. Fifty host CPU-delta snapshots bracket all measurement commands.
Observed `dotnet`/`clang` peak was 1.41%, with no heavy compiler overlap observed.
Ordinary desktop/VM activity remained; two-second sampling cannot exclude every
short task or make this a dedicated bare-metal benchmark. All samples are retained;
none was discarded. Output equality gates the suite, but timing ratios do not.

Scratch capacity in steady samples was 16/32/64 KiB, with 64 KiB peak. The peak sum
of charged managed storage and scratch was 513,520 bytes. These counters record
current/capacity high-water values, not cumulative allocation. The normal Instance
is 200 bytes (+56 versus baseline), separately from the dynamic scratch budget.

## RSS methodology correction

All six clean processes reported the same `wait4.ru_maxrss` of 42,476 KiB. This is
**not isolated native-runtime RSS**. A separate diagnostic reconstructed byte-
identical source and executable hashes, then sampled `/proc/<pid>/status` only
after `/proc/<pid>/exe` matched the native executable. Both child `wait4` values
were 38,736 KiB, exactly the launcher's recorded `VmHWM`, while observed post-exec
high-water values were 4,412 KiB baseline and 4,464 KiB prefix (1,730 and 362
samples). This confirms that the launch path can carry a large pre-exec high-water
floor. Those post-exec values are **sampled lower bounds from separate diagnostic
runs**, not exact whole-lifetime peaks or a precise 52 KiB memory-cost claim.

The reusable harness now explicitly labels `wait4`'s scope and provides a separate
memory recipe. Earlier immutable reports' process-RSS values, including the
original .NET/native comparison, must not be interpreted as runtime-only memory
without post-exec isolation. Their raw numbers remain historical evidence; this
finding does not invalidate their independently timed compute totals. The memory
probe's polled execution is never used for clean compute timing.

## Remaining scope

Compatibility independently reviewed the final source and authored cases and found
no remaining concrete ownership/corruption or stack blocker for this bounded
experiment, allowing controlled timing after coordination. It did not approve
production adoption. The nested allocator-hook test exercises a nested batch, not
arbitrary reentrant managed allocation/GC; list mutation during queries is excluded. The other feature review lane has not approved adoption.
This experiment is pinned to the old measured compiler, not the current feature
head. It has not established 32-bit support, arbitrary native-memory corruption
safety, cross-host performance stability or an acceptable default production
scratch budget. The retained timing suite is informational, with output equality
as a gate and no speed-ratio threshold.
