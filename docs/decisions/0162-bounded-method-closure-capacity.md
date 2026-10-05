# ADR 0162: Bounded method closure capacity

Status: Accepted

## Evidence and decision

After removing repeated binding work, the concrete kernel reaches the 256-method
cap with 12,073 IL bytes, 4,975 instructions and 16,216 metadata work units remaining.
This is a real declaration-count bound, not an exhausted metadata ledger. Increase
the shared ProgramLimits.MaximumMethods to 512 across discovery and detached
validation. Keep 1 MiB IL, 65,536 instructions, metadata work, aggregate IR storage,
call depth, native frames and all other limits unchanged. Declarations, interface
facts and executable bodies continue to share the cap.

Improve the aggregate discovery diagnostic to report method count, bytes,
instructions and remaining metadata work, making the limiting resource visible.
No native representation, instruction semantics or runtime ABI changes.

## Validation and review

A generated metadata-only fixture with 512 reachable methods compiles and runs at
O0/O2 with ASan/UBSan, forced collection, frame exhaustion and cleanup checks.
Its authored sum is checked without executing the input image in the compiler.
513 methods fail with the explicit limit diagnostic; detached excess also fails.
Interface plan tests accept 512 combined declarations/facts and reject 514, while
composed body/declaration tests reject an over-budget union. All 74 focused/composed
checks and the full solution build pass. Review checked every use of MaximumMethods,
remaining aggregate ledgers and bounded native frame behavior.

The unchanged kernel now reaches unsupported ResolutionStatus enum layout. Native
transcript equivalence and implementation/filter closure remain pending.
