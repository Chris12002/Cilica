# ADR 0222: Collector cleanup fused with sweep

Accepted 2026-10-20. Native v115.

Establish clean collector scratch outside marking as an explicit internal
invariant. Object construction initializes marked=false and mark_next=null;
ordinary object/array/string allocation uses those defaults, and pooled string
reuse assigns a fresh StringObject value. A marking pass no longer clears the
whole inventory before traversal. Successful collection clears surviving objects
during its existing sweep, then disarms the destructor's redundant cleanup walk.
The private state flag is named cleanup_pending to express this obligation.

Every failure after scratch preparation and every report-only/stopped pass still
uses MarkState's full cleanup. Busy and invalid-inventory exits occur before
scratch writes. The membership index remains disabled before reporting/sweeping
and on every exit; allocation failure still uses checked list membership. Full
inventory validation, precise edges, edge/object limits, public handle checks,
heap accounting and atomic no-sweep-on-failure semantics remain unchanged.
Generated destructors cannot invoke managed callbacks; reporting callbacks retain
the existing prohibition on mutating runtime state, including scratch. Arbitrary
private scratch corruption is not a supported managed operation.

Review audited all object allocation/header reuse sites and marking writes/exits.
Extended membership-index probes check clean scratch after every successful,
failed, limited and stopped public pass, including scratch allocation failure and
rebuild after reclamation. String-pool probes check clean new/reused headers.
All 36 tracing, index, collection, pool, allocation-verification, shared-module and
character-helper checks pass, including O0/O2, diagnostics and ASan/UBSan.


Correctness, shared-module, constrained-stack and release timing evidence is in
[the acceptance report](../performance/v115-collector-20261020.md).
