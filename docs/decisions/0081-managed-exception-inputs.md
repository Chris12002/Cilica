# ADR 0081: Validated managed exception inputs

Status: accepted for metadata snapshots; executable handlers remain next.
Recorded 2026-10-04.

`ReadManagedExceptionMethodInput` reads a closed method and retains bounded clause
boundaries, scoped catch signatures and stage-A filter classification. It reuses
the existing structural control-flow analysis for protected transfers and stack
depths. Call shapes are resolved as metadata in the caller's generic environment;
no input code is executed. Region count is capped at 256, declaration/call/region
work uses one remaining metadata budget, and failures expose no partial input.
General filters and indirect function-pointer calls remain explicit diagnostics.

Review found that the filter classifier's opcode whitelist allowed a local read
before a write in the current filter evaluation. Handler-local state could thereby
enter a purported type-only filter. A bounded definite-assignment intersection now
requires every reachable local read to follow a write on all incoming paths.
Cycles still fail the stage-A gate; acyclic backward branches remain eligible.
Exceeding the 65536-unit assignment analysis budget conservatively classifies the
filter as general. Existing restrictions prevent external use of filter-local
storage and taking its address.

Seventy distinct selected local checks cover real C# catch/finally/rethrow/type-test
filters, general-filter rejection, exact metadata capacity, illegal handler entry,
prior-state local reads and branch joins, plus existing region/stack/audit behavior.
This metadata gate neither creates managed exception objects nor executes handlers;
IR region contracts, rooted native propagation and CIL lowering remain subsequent
parts of milestone 5. No hosted work ran.
