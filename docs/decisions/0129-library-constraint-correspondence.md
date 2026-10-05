# ADR 0129: Library generic constraint correspondence

Status: Accepted

## Decision

Compare generic constraint sets for mapped type and method declarations after
projecting every named type through the explicit library plan. Compare open
declarations before substituting caller arguments. Retain separate type/method
variable environments, exact parameter flags and owner/index checks. Constraint
row order is immaterial; duplicate projected constraints are rejected.

Do not erase custom modifiers, admit unmapped contract types or accept open
generic definitions and out-of-range variables. Existing member-work ledgers
bound all declaration reads, structural validation and projection. No executable
or intrinsic authority is implied by matching declarations: closed snapshots
still prove argument constraints, and unsupported execution semantics remain
diagnostics. Ordinary strict resolution remains unchanged.

## Evidence and review

All 146 focused local checks pass. Thirty new correspondence checks cover type
and method environments, reordered rows and token padding, changed/added/removed
constraints, malformed arity/variables/signatures, duplicate obligations,
unmapped identities and exact/short metadata budgets. Existing mapping, scoped
compilation, intrinsic admission, implementation audits and closed-interface
constraint tests remain green.

Review confirmed that projected equality does not bypass executable constraint
validation or grant library trust. The nongeneric plan budget regression now
includes both generic-owner reads. No runtime ABI change or hosted work occurred.
Actual framework implementations and native kernel equivalence remain pending.
