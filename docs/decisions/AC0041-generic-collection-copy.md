# ADR AC0041: Generic collection copying and array compatibility

Accepted 2026-10-07 for bounded AC3.7.

Add the exact ICollection<T>.CopyTo array dispatch recipe for Int32, references
and admitted inline values. The helper receives the interface element view while
retaining both actual array descriptors. List copying shares the same managed
prefix implementation. Validate destination null, negative index and space before
whole-array compatibility, then perform the existing copy-specific element stores.
Unrelated class arrays fail even when empty. Related classes and interface arrays
allow per-element casts, preserving partial writes and exact dynamic cast messages.
An empty List<Base> therefore differs from an empty Left[] copied into Right[].

A private independently granted CopyCompatible intrinsic validates the original
selected-core declaration and caller. Both detached IR stages validate typed array
operands and fault edges. Native code uses a bounded static table of allowed pairs
from the validated closed catalog; it allocates no managed memory. Split receiver
checks use existing step boundaries, retaining the 2,048-byte stack-frame limit.
Array helper dispatch validates exact source/destination view, index and void result.
Native profile v45, object operations IR v18, integer-object CIL v6 and interface
plan v7 record these changes. Exception IR v5 and layout v17 remain unchanged.

Nine authored probes pass 2,646 repeated native/.NET comparisons on macOS ARM64,
O0/O2, ASan/UBSan, forced collection and strict cleanup. Six existing list probes
and the raw null-receiver regression add 2,058 comparisons. Grant removals, private
external calls, unsigned aliases, helper packet mutations and operation mutations
at both stages remain diagnostic. 58 selected array-copy, interface, value-array,
mutation and public baseline checks pass; the final empty-list pair also passes.
Initial compile/unsupported-expression/stack-frame failures and corrected runs are
preserved in artifacts/alchemy-array-copy. Review checked compatibility symmetry,
empty arrays, interface casts, arithmetic bounds, partial stores, root lifetimes,
exact grants and fail-closed detached validation.

General Array.Copy, section/nongeneric copying, primitive widening and boxing copies
remain unsupported. List(IEnumerable) still blocks the frozen engine, so no
redundant frozen closure refresh is claimed. Full engine/state/replay is unverified.
