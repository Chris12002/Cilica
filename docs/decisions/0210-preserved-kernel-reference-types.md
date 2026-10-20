# ADR 0210: Preserve validated reference types within native kernels

Status: accepted. Native v103. Recorded 2026-10-20.

The v101 investigation measured 12.06% of application samples in descriptor
predicates and 8.94% in array-storage validation. Most array validation was inside
Dictionary.Find. The same typed field, local and array references were repeatedly
checked across the non-allocating helper chain.

Use the validated IR's declared types throughout the existing closed kernel
proof. This is type preservation, not a claim that references are non-null:

1. Emission first validates logical/scalar IR through root assignment. Assignments,
   block arguments, field/array operands, call arguments and results must satisfy
   the existing assignability rules.
2. `_typed` entry wrappers retain reference descriptor and borrow storage/type/
   permission/lifetime checks. Array inputs, including borrowed array snapshots,
   additionally validate backing storage before entering a kernel.
3. Parameters, default-initialized locals, local writes, block edges and returns
   preserve their declared reference types. Managed fields and covariant array
   loads preserve the declared result type because allocation and managed stores
   maintain those storage invariants.
4. Eligible kernels cannot allocate, call unknown code, mutate external reference
   storage or escape a location. Selected virtual targets retain exact descriptor
   guards and the frozen acyclic proof. Kernel calls therefore preserve these
   facts without re-entering an unchecked boundary.

Normal kernels omit repeated receiver/result descriptor predicates and array-layout
validation. Keep null guards at uses, unsigned index bounds, exact virtual dispatch,
depth accounting and scalar-output staging/alias handling. Failed managed operations
still replay the framed method, preserving exception ordering and prior writes.
Cold/unproven paths, public handles, GC and ordinary framed execution are unchanged.
Future additions to the kernel whitelist must preserve this closed proof.

`CILICA_VERIFY_INTERNAL_REFERENCES=1` restores internal descriptor, membership and
array-layout diagnostics. A malformed backing allocation found at entry or by a
diagnostic kernel sets invalid-state fault 6 before fallback: ordinary framed
array reads trust backing storage and cannot safely replay that corruption.
This was exposed by the new negative test; it is a private corruption condition,
not a managed array bounds/null exception. Raw kernel calls are internal and
require the same preconditions as calls emitted by the compiler.

This follows the retained Roslyn IL's typed fields, locals and array operations.
Matching .NET JIT/Unity output in the v101 investigation uses native typed entries
and ordinary indexed reads, with null/bounds semantics rather than repeated heap
representation checks. No compiler-source revision match to the exact Roslyn
binary is claimed. Cilica's runtime, dictionary algorithm and GC remain independent.

Review covers validation-before-emission, null/default values, loop/block joins,
covariance, mutable own locals/parameters, staged aliases, reference borrow
snapshots, exact dispatch, fallback safety and translation-unit linkage.
Fresh paired timing is 58.054 us/diff for v102 and 48.767 for v103, 16.00% less
time. See [the performance report](../performance/typed-proof-performance-20261020.md)
for validation, exact source hashes, failures/repairs and reproduction.
