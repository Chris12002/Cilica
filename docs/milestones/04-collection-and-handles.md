# Milestone 4: Precise collection and handles

Status: in progress; precise reference visitation is implemented, collection and
persistent managed handles are not. Recorded 2026-10-03.

The baseline remains single-threaded, non-moving mark-and-sweep, using the phase A
shadow stack and exact reference maps from milestone 3. Retained allocations must
not be mistaken for roots. No managed reference crosses the packed Int32 boundary.

| Slice | Outcome | Status |
| --- | --- | --- |
| 4a | Precise root and object-edge visitation with provenance and bounded failure | Complete for private native enumeration |
| 4b | Bounded marking, cycle/alias handling, reachable-set diagnostics and failure semantics | Planned |
| 4c | Reclamation, exact heap accounting and allocation-triggered collection/retry | Planned |
| 4d | Collection-before-every-allocation stress and poisoned reclamation regressions | Planned |
| 4e | Instance-owned retained handles with stale/foreign-handle rejection | Planned |
| 4f | Composed integration review and supported-boundary consolidation | Planned |

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
The next slice adds bounded marking and explicit failure handling. Reclamation,
allocation retry, collection stress and handles require their own later evidence.
Multiple simultaneously active contexts within one instance are also outside the
current execution model and require a registration/coordination contract.

Review and validation: the final solution build has zero warnings/errors; all 194
selected local regression checks pass, including native/example regressions and
programs without String catalogs. The final O0/O2 tracing runs pass ASan/UBSan;
maximum measured static frames are 544/432 bytes respectively, below 2 KiB.
Local main remains an ancestor. No push, hosted work or additional agents ran.
