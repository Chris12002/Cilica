# ADR 0052: Collection before allocation and poisoned reclamation checks

Status: Accepted for private native v13 diagnostics. Recorded 2026-10-04.

The instance's opt-in `collect_before_allocation` flag requests a complete
collection at each eligible allocation attempt, before checking remaining heap
room or constructing an unpublished allocation. It shares the one-attempt budget
with pressure/native-failure retry. Cached literal hits and concat operations that
return an existing operand are not allocations. Normal execution remains pressure
triggered. Fault ordering and existing phase A root publication are unchanged.

Ten authored workloads cover cycles, nested allocating calls, reference/integer
arrays, overwritten loop temporaries, immutable string buffer owners, identity,
class/interface dispatch and ordered faults. Run each twenty times in one instance,
compare results to .NET, compare fault origins with ordinary native execution, and
collect after every return/unwind. Check no frames/execution storage remain and
that the surviving inventory agrees with marking and recorded byte charges.

ASan/UBSan run at O0/O2 with the existing 2 KiB static frame cap. A separate check
observes ASan poisoning on a reclaimed array header and its owned buffer, then
publishes the stale address and requires invalid tracing without dereferencing it.
This uses sanitizer-provided delete poisoning, not writes through freed memory.
Four selected tests passed; review found no phase A rooting defects in this corpus.
Other platforms, weak references, finalizers and phase B liveness remain untested
or unimplemented. No hosted work, push or additional agents ran.
