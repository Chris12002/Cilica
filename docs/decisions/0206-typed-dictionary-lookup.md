# ADR 0206: Typed execution through dictionary lookup

Status: accepted. Native v99. Recorded 2026-10-20.

The measured dictionary lookup chain repeatedly built execution frames and borrow
carriers around small methods. Extend the existing typed-kernel proof to field and
array reads, reference results, void helpers and selected virtual/interface targets.
This covers Dictionary HashKey/Hash/CheckKey/Find/TryGetValue/Equal and the proven
string comparer chain without changing the owned dictionary algorithm or Roslyn IL.

Each virtual call compares the exact receiver descriptor against proven targets.
Targets are frozen when the caller enters the acyclic proof, so later discoveries
cannot introduce recursion or increase its accepted cost. Boxed implementation
targets are excluded. At most 16 targets per call, eight nested kernels and 512
expanded statements are admitted; individual limits remain four parameters, eight
locals, 16 blocks and 96 statements/inputs. The expanded budget increases from 256
after testing the full workload on the constrained stack. Runtime depth is still
counted against 64. Unproven targets return to the original framed implementation.

Kernel code cannot allocate, publish heap/static/array writes, mutate reference
borrows or return an address. String literals and exception construction stop the
speculative path before allocation. All faults replay the original method, keeping
managed exception order, origin and prior writes. The wrapper's original by-value
arguments remain intact when a kernel changes its own parameters or locals.

Incoming borrows retain carrier, storage, lifetime, permission and exact type
validation. Read-only values can be snapshotted. For a method that may write, scalar
borrows must resolve to caller frame slots. Those slots are copied into native
temporaries, equal storage addresses share one temporary, and successful completion
commits the outputs. Failure discards all staged changes before framed replay.
Heap/array scalar outputs use the framed path: staging them would otherwise hide
writes from direct field/array reads of the same storage. Reference writes are
limited to the kernel's own by-value parameter/local addresses. The write effect
is conservatively propagated through the entire selected call graph.

Field/array reads retain null, descriptor, storage and bounds validation. No
safepoint can occur between validation, native execution and output commit. Caller
roots therefore retain all accessible objects, and normal generated result stores
root reference results before subsequent allocation. Public handle/GC checks and
the original fallback entry points remain unchanged. Translation-unit modules use
the same declared kernels and shared descriptor identities.

Self-review covered native pointer types, integer bit preservation, staged aliases,
heap alias fallback, frozen dispatch closure/costs, reference lifetime, fallback
side effects and exception order. The 59-check focused regression run passes,
including five new lookup checks. The four O0/O2 and typed-on/off variants perform
1,176 repeated native/.NET comparisons with ASan/UBSan, forced collection, exact
cleanup and frame-capacity checks. Cases include same-slot writable aliases,
heap output aliasing, mutation before a fault, null/bounds faults, a reference
result surviving allocation and an allocating/mutating interface implementation.
The fifth test separately compiles and links module translation units.

All 523 DiffPlex observations match in release and in the forced-GC diagnostic
build on a requested 512 KiB thread stack (checked within 512–576 KiB). The largest
release native frame is 432 bytes. Paired release measurements improve from
124.646 us/diff for v98 to 72.536 us/diff for v99, 41.81% less time. The separate
combined comparison measures v97 139.892 us versus v99 71.659 us. See
[the retained performance report](../performance/descriptor-typed-20261020.md).
These measurements establish this workload on macOS ARM64, not other targets or
arbitrary native stack usage. No public ABI, intrinsic grant or core-library change.
