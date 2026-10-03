# ADR 0050: Bounded native marking and reachability diagnostics

Status: Accepted for private marking; reclamation remains planned.
Recorded 2026-10-04.

## Algorithm and storage

Native v11 adds `mark_reachable` on top of ADR 0049's precise visitors. It validates
the retained allocation inventory, then follows published frame and literal roots
through inherited reference fields and reference-array elements. Nulls and aliases
consume edge work; each newly discovered object is marked before being put on an
iterative work list, so cycles terminate and each reachable object's children are
scanned once. The retained allocation list is an inventory, not a root source.

Store the mark bit and work-list link in each allocation's header. Keep them
separate from ownership/allocation links and the reserved synchronization word.
This avoids both a native recursive traversal and allocating a work queue while
under heap pressure. Header growth is included automatically by the existing
`sizeof`-based allocation quotas; it reduces how many objects fit in a byte budget.
It is not uncharged scratch memory. No managed object layout fields, reference-map
entries, or public ABI are added. The instance also has a marking-in-progress guard.

Default private limits are 4,096 retained objects and 262,144 delivered root/child
slots. Callers can supply explicit limits, including zero. The object cap covers
all retained objects, not just reachable ones. Inventory length must agree with
its counter; count guards reject readable cycles. Every header must have the
instance owner and a canonical known descriptor, including unreachable headers.
As in the visitors, allocation-list storage and buffer extents are runtime-owned
invariants, not arbitrary native-memory input. Only reachable outgoing edges are
validated; unreachable objects are classified without scanning their children.

Root/child ownership checks still use the bounded allocation list. Work is bounded
but is not claimed linear: these membership searches can multiply object and edge
costs. A visitor validates a value before invoking the edge-budget callback, so the
edge that exhausts the budget can incur one additional validation. Existing frame,
slot and literal-cache bounds remain. Marking itself allocates no memory.

## Completion and failure contract

Return `complete`, `invalid`, `limit`, `stopped` or `busy`. A per-instance guard
rejects a nested pass before it can change the outer pass's scratch state. The
supported execution model remains one active managed context per instance.

A scope guard clears all prepared mark bits/work links and releases the marking
guard on success, invalid reachable input, edge exhaustion or diagnostic stop.
Early inventory/size failures occur before scratch writes. Heap counters, object
contents, ownership links, synchronization state, root frames, execution storage
and pending fault/origin remain unchanged. Repairing input or increasing a limit
permits a fresh attempt. No allocation-triggered marking or retry is enabled yet.

Only after the entire graph validates may an optional callback receive each retained
object and its reachable/unreachable classification. It sees each allocation once;
callback order is inventory order, not a promise of a particular traversal path.
Callbacks may keep external bookkeeping, but cannot mutate runtime state (including
marking scratch), allocate managed objects, free objects or re-enter managed execution. Returning
false stops delivery; a diagnostic prefix may already have been delivered.

The returned report publishes reachable/unreachable and visited-slot counts only
on complete success, including completion of optional diagnostic delivery. All
other statuses return zero counts. Mark bits are cleared before returning even on
success: the report describes the observed point in time, not permission to sweep
later after roots or edges change. A future sweep must consume a fully successful
mark phase while the same protected graph remains unchanged. Retention-path and
named-root reports are not added by this slice.

## Validation and review

Extend the existing tracing harness instead of introducing another independent
fixture runtime. Its test-owned iterative visitor consumer and explicit expected
graph serve as the reachable-set oracle. Compare classifications for inherited
fields, aliases, cycles, arrays, literals and retained garbage; verify changed
roots produce new results and that marking needs no allocation headroom.

Exercise every edge limit below the small graph's exact requirement, exact/short
object limits, empty heaps and zero budgets, diagnostic stop and same-instance
reentry. Reject invalid reachable values, bad unreachable descriptors and incorrect
inventory counts/topology without exposing classifications. Check clean scratch
and successful retry after failure, and preserve pre-existing managed fault origins.
A 4,096-node aliased cycle exercises deep iterative traversal at the default object
cap, exact edge work, limit failure and a subsequent all-unreachable result.

The authored .NET/native probe now marks at each allocation observer as well as
walking real published frames. It compares the marked count to the independently
visited graph, including nested calls, constructor initialization, string operations
and every forced allocation-failure stage. After unwinding, only literal roots
remain. O0/O2 use ASan/UBSan, warning errors and static frame measurements. Review
split a large diagnostic test helper to preserve the existing 2 KiB frame limit.
No object has been reclaimed and no collector-stress or wider-platform claim follows.

Final local validation passed all 194 selected native/example regression checks and
the solution build with zero warnings/errors. The focused O0/O2 sanitizer runs
passed with maximum measured static frames of 688/496 bytes, below 2 KiB. Review
found no remaining issue within this marking scope. Local main remains an ancestor;
no push, hosted work or additional agents ran.
