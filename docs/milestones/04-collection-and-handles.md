# Milestone 4: Precise collection and handles

Status: complete for bounded native v14 on the locally tested macOS ARM64 target.
The public embedding ABI and broader managed runtime remain planned. Updated 2026-10-04.

The baseline remains single-threaded, non-moving mark-and-sweep, using the phase A
shadow stack and exact reference maps from milestone 3. Retained allocations must
not be mistaken for roots. No managed reference crosses the packed Int32 boundary.

| Slice | Outcome | Status |
| --- | --- | --- |
| 4a | Precise root and object-edge visitation with provenance and bounded failure | Complete for private native enumeration |
| 4b | Bounded marking, cycle/alias handling, reachable-set diagnostics and failure semantics | Complete for private native marking |
| 4c | Reclamation, exact heap accounting and allocation-triggered collection/retry | Complete for private native collection |
| 4d | Collection-before-every-allocation stress and poisoned reclamation regressions | Complete for local O0/O2 sanitizer corpus |
| 4e | Instance-owned retained handles with stale/foreign-handle rejection | Complete for private native helpers |
| 4f | Composed integration review and supported-boundary consolidation | Complete for the bounded native profile |

These are capability boundaries, not a schedule. Split them further when review
finds an independently testable ownership or failure contract. No phase B liveness
optimization begins before phase A survives real collection stress.

## Slice 4a outcome

[ADR 0049](../decisions/0049-precise-reference-visitation.md) adds private native v10
visitors for the supplied context's published frame slots and the instance's
literal cache, plus each allocation's inherited reference fields and reference-array
elements. Nulls and aliases retain slot identity. Integer storage, string payloads,
descriptors, ownership links and the heap allocation list are excluded.

Callbacks receive borrowed provenance records. Each contained managed value is
checked for ownership and exact known descriptor identity before delivery. Walkers
are allocation-free and nonrecursive, preserve fault/origin and heap state, and
separate complete, stopped and invalid outcomes. Incomplete callbacks may have seen
a prefix and cannot authorize sweeping. Frame and literal-list guards enforce the
existing limits; runtime-owned native storage/layouts remain trusted invariants.

Tests compose the visitors using an independent iterative consumer: a graph with
cycles, aliases, inherited fields and arrays retains only objects reached from
actual roots, even though all allocations still exist. Real generated frames are
examined at every allocation in an authored .NET/native probe, including nested
calls, partially initialized receivers, string allocations and forced heap-limit
faults. Exact/excess depth, root-slot and literal-cache limits, invalid contained
pointers/descriptors, early termination and clean fault unwinding are covered.

The runtime still retains every allocation until teardown. The visitors are not
called as a new mandatory allocation pass; tests use the existing observation hook.
Slice 4b below adds bounded marking and explicit failure handling. Reclamation,
allocation retry, collection stress and handles require their own later evidence.
Multiple simultaneously active contexts within one instance are also outside the
current execution model and require a registration/coordination contract.

Review and validation: the final solution build has zero warnings/errors; all 194
selected local regression checks pass, including native/example regressions and
programs without String catalogs. The final O0/O2 tracing runs pass ASan/UBSan;
maximum measured static frames are 544/432 bytes respectively, below 2 KiB.
Local main remains an ancestor. No push, hosted work or additional agents ran.


## Slice 4b outcome

[ADR 0050](../decisions/0050-bounded-native-marking.md) implements private native v11
marking with an intrusive work list and separate header mark bit. Mark before
enqueueing; process each reachable object's children once. Cycles and aliases
terminate without recursion or a scratch allocation. The existing heap byte quota
includes the larger headers. Ordinary allocation still does not trigger marking.

Validate the full retained inventory before scratch writes. Bound retained objects
and root/child slot work (defaults 4,096 and 262,144), with nulls and aliases charged.
Use existing precise ownership/descriptor checks. An optional diagnostic callback
classifies every retained object only after the reachable graph fully validates.
Successful reports include live/dead and slot counts; invalid, exhausted, stopped
or nested passes publish no counts. Diagnostic cancellation may expose a prefix.

Scoped cleanup clears marks/work links and the per-instance guard on exit. Preserve
managed fault/origin state and all ordinary heap/root data. A complete report is
an observation, not a durable authorization to reclaim later after graph mutation.
The next slice must combine a complete mark phase with sweeping under one unchanged
graph, exact allocation accounting and explicit allocation-failure/retry semantics.

Tests cover explicit and independently visited reachable sets, changed roots,
all small-graph edge-exhaustion points, exact/short object budgets, callbacks and
reentry, malformed reachable values/inventory, scratch cleanup and retry. A
4,096-object aliased cycle exercises bounded iterative marking. Real allocation
observers compare native marking with precise visitation throughout the authored
.NET/native probe and forced allocation faults. No reclamation, persistent handles,
phase B or multi-context execution is enabled.

Review and validation: all 194 selected native/example regressions pass, and the
final solution build has zero warnings/errors. Focused O0/O2 ASan/UBSan checks pass;
maximum measured static frames are 688/496 bytes, below 2 KiB. Review found no
remaining issue within this scope. Local main remains an ancestor. No push, hosted
work or additional agents ran.


## Slice 4c outcome

[ADR 0051](../decisions/0051-native-reclamation.md) joins marking and sweeping under
one guard. Incomplete traces free nothing; successful passes reclaim unreachable
cycles, arrays and strings using typed destructors and exact recorded byte charges.
Allocation retries after at most one collection on quota/native allocation failure.
Direct native borrowers must publish roots across allocations. Focused O0/O2
tracing, array and string sanitizer regressions pass; static frame limits remain
2 KiB. Review corrected the array harness's formerly unrooted native borrower.
Local main remains an ancestor; no hosted work or push ran.


## Slice 4d outcome

[ADR 0052](../decisions/0052-collection-stress.md) adds opt-in collection before
allocation. Ten workloads run twenty times per instance, comparing .NET values and
ordinary native fault origins while exercising cycles, loops, nested calls, arrays,
strings and dispatch. Reclaimed headers/buffers are confirmed ASan-poisoned; stale
roots are rejected before dereference. All four focused O0/O2 sanitizer checks pass
with the 2 KiB frame cap. Review found no rooting defect in this corpus. No hosted
work, push or extra agents ran.


## Slice 4e outcome

[ADR 0053](../decisions/0053-instance-retained-handles.md) adds a bounded lazy table
of strong roots with nonreused opaque token generations. Resolve borrows a value;
release removes only that handle's root. Duplicate/null handles, foreign/stale
rejection, slot and instance-address reuse, capacity/edge budgets, token exhaustion,
and retention across stress allocations are tested. Review found no remaining
ownership or token-reuse issue. Local O0/O2 ASan/UBSan checks pass; no public handle
ABI, wider platform claim, hosted work, push or additional agents are introduced.


## Slice 4f outcome and completion boundary

[ADR 0054](../decisions/0054-stage-four-completion.md) closes milestone 4 with the
composed inventory example running both ordinary packed calls and a persistent
private instance under collection stress. Results and fault origins match .NET;
post-unwind collection verifies no unreachable allocations survive. Application
rules are unchanged. Quota regressions now distinguish live-object requirements
from cumulative allocation counts and still verify fault/frame cleanup and reuse.

Final local regression run: **196 passed, zero failed/skipped**. O0/O2 ASan/UBSan
checks include cycles, aliases, inherited fields, arrays, UTF-16 strings, dispatch,
loops, nested calls, fault unwinding, poisoned reclamation and handle lifetimes.
The tracing/collection/handle harness's measured static frames are 768/560 bytes,
below 2 KiB. Solution build: zero warnings/errors. Each slice was reviewed before
its commit; final review found no remaining issue in the supported scope.

Milestone 4 implements non-moving collection and private handles for the existing
bounded types, with phase A roots and a sole active context per instance. No claim
is made for a public persistent ABI, mobile/web execution, finalizers, weak roots,
concurrent/generational collection or phase B liveness. See ADR 0054 for remaining
diagnostics and resource limits. Local main remains an ancestor; no push, hosted
work, paid service or additional agents ran. The next milestone is 5: staged value
types, boxing, generics, delegates and exception semantics.


## Follow-up review before milestone 5

Reviewed the milestone 4 diff from `59df625` through `440385d`: tracing and mark
limits, atomic mark/sweep, allocation partial-failure cleanup/retry, precise roots
and retained-handle ownership/generation lifetime. No blocking defect found in
its supported single-context private runtime. Six focused local tests passed,
covering tracing/handles, collection stress and the composed example at O0/O2
with ASan/UBSan. This is targeted confirmation, not a new full-suite/platform claim.
Local main remains an ancestor; the remote currently advertises no main branch.
No push, hosted run or additional agent was used.
