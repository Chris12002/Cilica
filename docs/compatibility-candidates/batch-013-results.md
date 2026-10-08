# Batch013: same-owner constructor-chain support

The original independent CircularBuffer constructor reduction now executes through
Cilica. Exact same-owner chains preserve argument/field-initializer order, faults,
private access, readonly stores and roots. Cycles remain explicitly unsupported;
the validator checks them iteratively in both representations. A narrowly typed
AddressArraySlot prefix rule repairs the lowered struct-array argument composition.
No runtime or emitter changes. CIL101/object IR61/native79.

**111/111 focused and adjacent checks passed in 57.121s.** Six new semantic roots and
the original independent reduction provide 2,058 repeated native/.NET comparisons.
Two native O0/O2 checks cover object limits 1..6, unwind, recovery and exact root
counts. Nine forged packets per representation test the constructor boundaries.
Adjacent hierarchy, pre-initialization escape, class values/borrows and ArraySegment
regressions pass. Native execution uses forced GC, ASan/UBSan and strict frame checks.
Full repository tests and Alchemy replay were not run.

Initial results were 6/10, then 10/12, then diagnostic 4/6. The failures identified the
lowered AddressArraySlot gap and a cycle-test setup intercepted by the independent
unused-method guard. All attempts remain in [evidence](batch-013-evidence.json), with
final source/assembly hashes and native source/executable receipts. The
[final log](batch-013/final-tests.log) and [decision](../decisions/compatibility-015-constructor-chains.md)
record the passing outcome and review boundary.

No actual CircularBuffer retry ran. Its first approved failure remains recorded in
[circularbuffer-execution-20261008.md](circularbuffer-execution-20261008.md). The human
approval was independently verified by the parent (message 01a11b66-eb59-737b-b724-c9025b5706de).
Per its stop-on-gate boundary, a revised frozen trial proposal is prepared separately:
same candidate source, same 48 scenarios and resource limits, new compiler only.
Do not mark the candidate verified until every observation matches normal .NET.

Provenance correction: the retained batch012 compiler DLL matches its recorded hash
and embeds CIL99. Its earlier CIL98 label missed a concurrent peer change; the record
now says 99 and retains all logs/hashes. Current peer CIL100 and documentation 890e14a
are preserved. No performance experiment was adopted, and no candidate cleanup was
needed in this owned-only batch.
