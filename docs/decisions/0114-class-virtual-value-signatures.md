# ADR 0114: Class virtual value signatures

Status: Accepted

## Decision

The explicit integer-object profile admits existing inline value copies, returns
and synchronous borrow parameters on exact class virtual slots. Signature
validation retains exact slot compatibility and borrow element/capability rules.
The shared lowering expands value arguments into scalar leaves and appends
caller-owned rooted return destinations. Dispatch maps and argument-before-null
semantics survive conversion of a value-return expression to a void invocation.
Zero-leaf structs still perform the call and receiver check.

The new conformance fixture also exposed required starg lowering. Reassignment
of a by-value parameter writes its current call's storage through the existing
checked location machinery, copying all value leaves and normalizing narrow
scalars. Borrowed-parameter rebinding and instance-receiver reassignment remain
diagnostics. This does not admit nested byrefs, escaping borrows or ref returns.
Legacy profiles retain their scalar virtual-signature boundary. Interface value
plans and metadata discovery remain the next gate.

## Evidence and review

Five C# probes match .NET in 1,470 repeated O0/O2 sanitizer results with forced
collection and exact/short frame budgets. They cover generic overrides, direct
base calls, reference-bearing wide structs, copied versus aliased values,
borrowed mutation, caught faults, argument/null order, empty structs and narrow
parameter reassignment. A negative check preserves borrowed-parameter rebinding
diagnostics. All 66 focused checks pass; final catalog gating is checked by the
related dispatch/IR regressions. Review found no remaining actionable issue.
