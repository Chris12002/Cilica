# ADR AC0025: Deferred Where

Accepted 2026-10-06 for bounded AC6.3.

## Decision

Implement Where<T>(IEnumerable<T>, Func<T,Boolean>) with eager source/predicate
validation and lazy enumeration. One owned WhereIterator<T> carries either an array
or a generic enumerable source. It preserves the pinned state transitions,
Current snapshots, first-managed-thread reuse, later clones, Reset fault and
Dispose behavior. Empty arrays return canonical Array.Empty<T>. Nested Where
combines predicates against the original source, preserving short-circuit order.
Array loops advance state before invoking predicates; generic loops retain their
enumerator across calls. A throwing Dispose leaves its state uncleared as pinned.

Any/Count query fast paths traverse the source without mutating the query's cursor
or current value. They dispatch through a nongeneric owned IQueryCardinality
contract. Direct generic WhereIterator<T> references in these broad methods were
rejected during review: even plain Count on a struct array then pulled unsupported
Func<Struct,Boolean> into its compiler closure. The nongeneric contract restores
that prior capability without weakening compiler admission or adding an intrinsic.
Helpers have public metadata for the existing generic visibility rule; they are
not mapped framework iterator contracts. Private upstream inheritance is flattened.

Int32/reference predicates and admitted arrays/reference enumerables are supported.
List-specialized span/enumerator behavior, boxed collection factories, indexed
Where, value-payload delegates and nongeneric Current remain unsupported. Later
operators must retain the private-optimization semantics applicable to their owned
query types. No compiler profiles or grants changed.

## Evidence and review

Ten authored probes over 72 pairs and three repetitions at O0/O2 supply 4,320
native/.NET comparisons. Public Where adds 294, totaling 4,614. Deferred source
mutation, same-thread reuse/clones, combined callback traces, cardinality during
active iteration, empty identity, fallback/disposal faults, Reset/null order and
resuming after a predicate fault all match. Thirty-six existing LINQ/baseline checks
pass, including restored struct-array counting and explicit unsupported overloads.
ASan/UBSan, forced GC and native process/frame budgets remain unchanged.

Four native GC checks prove ownership. A live query retaining the old Current and
an updated source slot has seven objects; disposal releases only old Current
(six), and dropping the query leaves just the emergency exception. The unchanged
public noncapturing lambda retains exactly two static objects (delegate and
receiver) per instance. Clearing one instance's static roots releases them without
affecting the other instance. Only this public probe uses the persistent-cache
harness option; all other baseline controls retain strict cleanup checks.

Review checked state advancement before callbacks, checked count arithmetic,
source/callback roots, exception/disposal precedence, query identity and factory
closure isolation. The baseline cache assertion and generic fast-path regression
were fixed and retested, not waived. The ledger has 18 gaps and 29 controls.
Private saved-game replay remains unfinished.

The final exact-cache test initially counted declared static slots instead of
occupied roots: array fallback discovery also declares an unused empty-enumerator
cache. Follow-up validation counts exactly two occupied roots, permits unpopulated
slots and still requires heap counts of three before clearing and one afterward.
Both O0/O2 checks pass; the test correction is a separate commit.
