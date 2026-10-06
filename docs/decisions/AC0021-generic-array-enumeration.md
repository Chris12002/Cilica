# ADR AC0021: Generic array enumeration

Accepted 2026-10-06 for bounded AC4.5.

## Decision

Add one exact ArrayInterfaceEnumerate grant for the ordinary managed
ArrayInterface<T>.Enumerate(T[]) helper. Select T from the requested IEnumerable<T>
contract, including widened reference arrays. Return the exact IEnumerator<T>:
a merely covariant substitute would change the per-T empty enumerator identity.
Detached logical and scalar plans authenticate the vector/result relationship;
the interface plan advances to v6. Native profile v41 is unchanged.

Adapt pinned SZGenericArrayEnumerator<T> and its private base into one owned
reference class. Nonempty enumeration stores the live array; Current reads the
current slot each time, including later array mutations. Reset returns to -1,
MoveNext ends at array length, and Dispose does nothing. Before-start/after-end
Current faults preserve the pinned messages. Empty arrays share a mutable
per-closed-T enumerator containing a null array; obtaining it does not reset it.
The lazy cache is owned and traced by each runtime instance.

Generic Int32, reference and admitted inline-value elements are supported.
UInt32 array aliases, nongeneric Current/array enumeration and mixed variant
class-dispatch closures remain diagnosed. The latter is an existing exact-mapping
boundary: a closure containing both IEnumerator<string> and IEnumerator<object>
requires variant class target selection that is not part of this slice. Boxed
list/dictionary/view interface enumerators are also still unfinished.

## Evidence and review

Seven authored probes plus the public ArrayEnumeration probe supply 2,352 repeated
native/.NET comparisons at O0/O2 on macOS ARM64. ASan/UBSan, forced collection,
30-second native process limits and the 2,048-byte frame limit remain enabled.
Four native lifetime runs prove a nonempty enumerator keeps its array and payload
after end/disposal, releases both when dropped, and empty caches retain no caller
arrays and remain isolated between runtime instances. Grant denial, three negative
boundaries and detached logical/scalar packet mutation checks pass. All 60 selected
existing library/baseline regressions pass; the ledger has 21 gaps and 26 controls.

Review checked unsigned index arithmetic, the terminal index, exact logical-T
selection, reference/value current semantics, null-before-body behavior, cache
state/identity, and roots across allocations. Initial tests were corrected to
separate interned exception strings from precise cache counts and to preserve the
known variant-dispatch diagnostic. No private saved-game replay is claimed.
