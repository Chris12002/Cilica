# ADR AC0022: Parameterless LINQ cardinality

Accepted 2026-10-06 for bounded AC6.1.

## Decision

Map System.Linq.Enumerable and implement parameterless Any<T> and Count<T> as
ordinary managed methods. Preserve pinned null validation, ICollection<T> before
nongeneric ICollection, then the reference enumerator fallback with using/finally.
Any makes one MoveNext call; Count uses checked Int32 increments. Neither reads
Current. Collection Count results are passed through unchanged, including unusual
negative counts from custom implementations. Exceptions from Dispose replace an
earlier MoveNext exception, while a factory exception never enters the using scope.

No compiler grant or profile changes are required. Pinned runtime-private Iterator
fast paths are omitted because no owned LINQ iterators exist yet. They must be
considered when deferred operators are added. Predicate overloads remain unmapped.
Array and custom reference-enumerator closures work; list/dictionary/view factories
still fail if their unsupported boxed enumeration body enters the closure, even
where a runtime count shortcut would avoid it. Do not claim arbitrary collections.

## Evidence and review

Eight authored probes over 128 input pairs and three repetitions at O0/O2 supply
6,144 native/.NET comparisons. Public LinqAny adds 294, totaling 6,438. Coverage
includes Int32/reference/value arrays, empty and nonempty fallback sequences,
generic-versus-nongeneric collection priority, null source, null/faulting factory,
MoveNext faults, disposal faults and exact callback traces. Current deliberately
throws in fallback fixtures to prove it is never read. All 35 selected existing
array/core/baseline regressions and the negative-overload/factory test pass.
Forced GC, ASan/UBSan, native process and frame limits remain enabled.

Review checked checked arithmetic, short-circuit count behavior, generic mapping,
using scope and exception precedence. Executing over Int32.MaxValue elements was
not attempted; overflow relies on the existing verified checked arithmetic path.
The ledger has 20 gaps and 27 controls. Enumerable mapping advances the remaining
Where diagnostic from missing type to missing method without claiming its support.
Full saved-game replay is still incomplete.

A separate substring source audit found that pinned negative-argument exceptions
carry ActualValue, which the current native exception payload lacks. Substring
remains a gap pending that exact range-fault prerequisite; no partial substring
implementation was added.
