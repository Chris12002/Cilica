# ADR 0223: bounded string-separator cache

Accepted 2026-10-20. Owned core algorithm change; native v115, runtime profile,
compiler, grants and public capability set remain unchanged.

Fresh DiffPlex v115 profiling attributes 31.34% of line samples to separator
matching. Roslyn emits the Split call; .NET and Unity library implementations
record separator positions and widths. The owned implementation searched twice.

Cache the first 64 matches in one lazily allocated, rooted Int32 array containing
position/width pairs. Allocate at most 128 elements (512 payload bytes plus the
array header), and allocate none when there is no match. Count outputs using the
existing rules. Materialize from the cached prefix; rescan only the remaining
suffix if the cache filled. This bounds additional memory without limiting input
length or number of output entries. Keep the char-separator overload unchanged.

The buffer uses existing managed allocation and quota accounting. Very tight heaps
can now fail earlier; preserve normal allocation faults, unwind roots and permit
recovery. No quota or stack limit is raised. This avoids adding stack/pool/span
support or an unbounded separator buffer just for the optimization.

Preserve declaration-order separator precedence, nonoverlapping consumption,
null/empty ignored entries, Unicode whitespace fallback, trimming/empty filtering,
result identity and fault order. Source text is immutable and the existing
single-threaded profile has no managed callback that can mutate separator entries
between scanning and output construction. No new concurrency guarantee is made.

Review establishes even bounded capacity, complete saved pairs and exact suffix
start; roots remain live across result/substring allocations. Boundary, identity,
GC, allocation-failure/recovery and shared-module checks pass alongside existing
semantic tests. Final line timing improves 15.86%, with 19.80% in the earlier
paired batch; character timing is effectively unchanged. All 37 focused checks
and both complete DiffPlex transcripts pass, including forced GC, ASan/UBSan and
constrained-stack execution. See the [full evidence and review](../performance/splitcache-20261020.md).
