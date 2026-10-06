# ADR AC0013: List and array search with exact equality receivers

Accepted 2026-10-06 for bounded AC3.4.

## Decision

Implement List<T>.Contains, IndexOf(T) and Remove(T), plus ReadOnlyCollection<T>
search forwarding. Search only live list elements and remove the first match via
the existing RemoveAt. Add separately granted generic array IndexOf/Contains
helpers and an unconditional fixed-size Remove helper. No equality call or index
validation precedes the array Remove fault. Interface plan v5 validates the new
logical/scalar argument shapes and Int32 carriers for index/Boolean results.

Use a fourth exact private grant, ClosedArraySearch, for KeyOperations<T>.IndexOf.
Authenticate its owner, call context, generic shape and complete declaration.
Select an ordinary managed CopiedArraySearch<T> or ValueArraySearch<T> body by the
closed element storage. The copied path uses existing local borrows for integer
and admitted reference equality. The value path borrows the actual array element
when calling the existing exact IEquatable<T> target. Both paths still require the
existing ClosedKeyEqual grant and use the caller's captured array/count arguments.
No general scalar/reference array addresses, new native operations or public ABI
are introduced. Nullable and arbitrary-reference fallback equality remain build
errors. Empty-value array Remove is admitted because it does not compare values.

The initial design incorrectly assumed value searches copy the equality receiver,
as dictionary equality does. Differential tests showed that .NET 10.0.11 array
search can mutate the stored value through Equals. A first direct-borrow revision
correctly met the compiler's existing scalar/reference element-address gate. The
reviewed two-body selection preserves each receiver behavior without weakening
that gate. Search callbacks may mutate elements even when they return false or
throw; this is observable behavior, not collection transaction rollback.

## Evidence and review

Twelve authored probes plus the public ListContains probe provide 3,822 repeated
O0/O2 .NET/native comparisons on macOS ARM64 with ASan/UBSan, forced collection,
frame budgets and cleanup checks. Coverage includes first matches/duplicates,
unoccupied capacity, null strings, value receiver mutation, throwing/allocating
callbacks, mixed list/array and live read-only views, fixed-size/read-only faults,
empty-value packets and argument-before-null behavior.

Two dedicated GC checks prove value removal releases a struct-contained reference
while its list stays rooted. Grant removal, hostile private callers, nullable and
unknown reference equality remain rejected. Logical/scalar packet mutations and
48 existing collection/array/admission/public-baseline tests pass. The GC harness
was corrected to retain its Clear helper and count the permanent Array.Empty cache;
production collection limits and GC code did not change.

Review checked exact search selection, reference-rooted element borrows across
allocation and exceptions, mutation visibility, captured array/count semantics,
first-match removal, vacated slots, covariance admission and all helper signatures.
Four grants are appended without renumbering existing capabilities. Native v41,
object layout v15, integer CIL v3 and embedding profiles remain unchanged.

The ledger has 28 gaps and 19 controls. Nullable/default-struct/arbitrary-reference
equality, range overloads, CopyTo, bulk APIs, remaining dictionary/set APIs and
enumeration remain separate work. No private replay root, hosted work, additional
agents or services were run.
