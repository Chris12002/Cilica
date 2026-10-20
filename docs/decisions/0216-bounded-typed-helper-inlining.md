# ADR 0216: bounded typed helper inlining across native modules

Accepted 2026-10-20. Native v109, following v108 small predicates.

The v107 investigation identified remaining native calls inside the proven typed
Dictionary lookup graph. Roslyn emits ordinary calls and local addresses; Unity
presents typed C++ calls and eligible helper bodies to Clang. Cilica's typed graph
already excludes allocation, callbacks, escaping borrows and external reference
writes, and bounds every eligible entry to depth eight and 512 expanded statements.
Use that existing proof to request optimized native inlining of kernel bodies.
See [the investigation](../performance/v107-profile-20261020.md).

Only `_kernel` declarations and definitions gain always_inline when `__OPTIMIZE__`
is defined. O0 retains its prior compiler-default kernel treatment. Eligibility,
managed depth accounting, input validation, temporary borrow staging, alias
coalescing, success-only output commits and framed fault replay are unchanged.
No allocation is performed inside a kernel, including paths that defer literal or
exception construction to replay. Wrappers and allocating helpers keep their
existing compilation policy. Disabling typed internals still selects framed calls.

Shared modules must expose bodies, not only prototypes, to the native compiler.
Split kernel emission from wrapper emission. For modules, all proven kernels are
emitted into the common header after runtime helpers, descriptors and method/kernel
declarations. Namespace-scope static becomes C++ inline through the existing module
builder, preserving one function/descriptor identity. Framed methods, their private
frames and typed wrappers remain in their individual source files. No LTO is
required. Single-file emission preserves its original order. The existing 64 MiB
shared-source cap remains and includes kernel definitions; this trades repeated
header parsing for cross-unit optimization without expanding graph eligibility.

Review covered declarations versus bodies, ODR/function identity, all cross-unit
references, no accidental movement of framed state, depth RAII on failed kernels,
unsigned arithmetic, rooted reference reads and speculative-write rollback.
Normalized DiffPlex output is byte-identical to v108 after removing the new macro,
attributes and profile label. Its 39 kernel bodies have no logic changes.

Forty-six focused checks pass. Shared-module tests independently compile one
method per file under O0/O2 ASan/UBSan, typed mode off/on and diagnostics off/on;
they compare 294 success/fault observations against .NET and verify kernel pointer
identity across units. Other checks cover mutable/aliased borrows, reference and
wide results, invalid storage, null/bounds faults, depth and arena exhaustion.
New branching/chain probes admit depth eight but reject nine, reject excessive
expanded size and recursion, and execute correctly through the retained fallback.
All 523 DiffPlex observations match release and both forced-GC sanitizer modes on
a checked 512–576 KiB stack. See [measurements](../performance/v109-typed-inlining-20261020.md).
