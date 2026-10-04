# ADR 0082: Managed exception region and root contracts

Status: accepted for detached authored IR. Recorded 2026-10-04.

`ManagedExceptionIrProgram` selects `managed-exceptions-ir-v1` around the composed
value/delegate program. Exception identities are explicit intrinsic fieldless shells
in the nominal class graph, rooted at a direct Object child. Runtime fault statuses
1, 2, 4, 5, 7, 10, 11 and 12 map to distinct exact exception identities; fatal native
ownership/depth/arena statuses are not managed exception types. Ordinary constructors
and instance bodies cannot target intrinsic shells. Creation and message access use
explicit IR operations, with optional selected-string message storage. Creation and
catch type access obey the existing visibility rules.

Methods retain ordered half-open block regions for catch, type-test filter, finally
and fault clauses. Throw/rethrow, leave, endfinally and endfilter have explicit
terminators. The verifier checks entry stacks, range nesting, handler placement,
protected transfers and matching clause exits. Normal return cannot bypass cleanup;
normal branches cannot enter handlers/filters or exit protected scopes. Existing
may-fault edges remain method-exit requests; the selected exception profile must
route those requests through the method's region dispatcher before actual exit.

Filters admit only acyclic type tests, comparisons, constants, bit operations,
branches and private locals. Every local read requires assignment during the current
evaluation on every incoming path; filter-local storage cannot escape into other
scopes or through a borrow. Classification work and all region counts are bounded.

Phase A source planning reserves two exception reference roots per clause for caught
identity and suspended propagation during cleanup/filter evaluation. These slots
count toward the existing 1024-root limit, alongside ordinary locals/values and
embedded references. The aggregate program region limit is 256. Native runtime
pending/emergency roots require a separate implementation gate.

Review checked invalid handler entry/exit, nested scopes, rethrow placement, filter
purity/cycles, type visibility, complete runtime identities and hidden root expansion.
All 138 distinct selected local verifier checks pass, including the reduced 1024-root
boundary. Earlier execution profiles reject region-bearing IR and exception operations.
Scalar lowering, native storage/dispatch and CIL handlers are not implemented by this
contract slice. No hosted work ran.
