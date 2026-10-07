# ADR AC0102: HashSet value and interface cursors

Recorded 2026-10-07 for AC4.9.

Enumerable set construction conservatively discovers the set's own interface
cursor even after its same-set copy branch. The constructor-only probe therefore
reaches the explicit unsupported GetEnumerator body. Implement that real dependency
instead of weakening interface closure discovery or inserting a runtime fallback.

Map the exact nested HashSet<T>.Enumerator declaration. Its ordinary managed value
stores the owning set, captured version, current entry index and copied current
value. Small storage readers expose the existing entry count, version, Next and
Value; no new native operation or grant is needed. MoveNext rejects insertion
version changes, skips free entries and records live count plus one on exhaustion.
Generic Current and no-op Dispose preserve the pinned behavior; nongeneric Current
checks the start/end state and Reset checks the captured version. Default cursors
retain their null-owner faults. Copies retain independent positions and values.

Generic and nongeneric interface factories return a boxed value cursor for a
nonempty set and the existing shared SZGenericArrayEnumerator<T> empty cursor for
an empty set. Empty-source identity and immunity to later set mutations are tested.
The existing supported payload profiles apply; Int64 vector/cursor closure remains
diagnostic and is not hidden by this implementation.

Seven probes supply 2,016 repeated native/.NET comparisons at O0/O2 with forced
collection, ASan/UBSan and exact cleanup. They cover free-slot order, value copies,
duplicate/successful insertions, removal/clear, mutation after exhaustion, Reset,
nongeneric Current, retained reference-bearing keys and default/empty cursors.
The empty-cursor probe requires exactly one additional static root. All 27 checks
pass in artifacts/alchemy-set-enumeration/ac4-9a-tests.log, including existing set
lifetime/grant boundaries and the 42-control/five-gap baseline. The obsolete
unsupported-enumeration fixture is replaced by positive execution coverage.

Review checked live count comparisons, copied Current rooting, default short-circuit
fault order, version behavior and exact nested contract mapping. Constructor
composition receives a separate final run and shared final hashes in ADR AC0103.
Compiler/native profiles, runtime layout and production limits are unchanged.
