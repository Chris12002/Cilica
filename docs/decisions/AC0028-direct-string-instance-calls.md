# ADR AC0028: Direct string instance calls

Accepted 2026-10-06 for bounded AC7b.3.

## Decision

Exact selected String.get_Length/get_Chars accept both call and callvirt in object
and composed value discovery. Roslyn emits direct calls for known literal receivers.
Both forms lower to the existing checked string operation, preserving null and
bounds faults. Declaration identity, complete signatures and intrinsic grants are
still mandatory; this does not admit arbitrary instance methods, overrides or
newobj. Static string operations still require call. No runtime, IR or grant change.

## Evidence and review

Two Roslyn literal probes and two raw direct-call probes pass 1,176 repeated O0/O2
native/.NET comparisons. Raw probes cover null receivers with valid/invalid indices,
bounds faults, embedded NUL and surrogate code units. The five constant-layout
probes now use direct literal Length calls and pass another 1,470 comparisons,
for 2,646 total. All 150 string metadata, object contract, composed native and
library admission checks pass. Sanitizers, forced GC and frame/process budgets
remain unchanged. Metadata-only direct calls also pass the older object translator.

Review checked selected-owner authentication, signature and call-form gates,
unchanged permission checks, null-before-bounds order and root/exception handling.
The previous direct-call negative test is replaced by positive exact-member checks;
wrong signatures, private/generic/static declarations and newobj remain negative.
Evidence: artifacts/alchemy-direct-string-calls. Public counts remain 18 gaps and
29 controls; saved-game replay remains unfinished.
