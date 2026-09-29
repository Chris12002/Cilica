# Architecture decisions

Each record states its context, decision, consequences, rejected alternatives
and how it will be validated. A proposed decision is not an implemented
capability. It becomes accepted once its validation experiment has run.

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-execution-and-threading-model.md) | Execution, async and threading model | Proposed |
| [0002](0002-gc-root-tracking.md) | Garbage-collection root tracking | Proposed |
| [0003](0003-class-library-strategy.md) | Class library strategy | Proposed |
| [0004](0004-exception-handling.md) | Exception handling | Proposed |
| [0005](0005-explicit-assembly-resolution.md) | Explicit assembly resolution | Accepted for resolver foundation |
| [0006](0006-structural-member-binding.md) | Structural signatures and declaration binding | Accepted for bounded binding profile |
| [0007](0007-dependency-graph-foundation.md) | Bounded dependency graphs with inclusion reasons | Accepted for ordinary dependency expansion |
| [0008](0008-conservative-dispatch-expansion.md) | Conservative dispatch over included named types | Accepted for bounded dispatch profile |
| [0009](0009-audit-evidence-contract.md) | Bounded audit evidence and explicit uncertainty | Accepted for metadata audit profile |
| [0010](0010-constrained-instance-dispatch.md) | Constrained instance-call dependency expansion | Accepted for closed named constraints |
| [0011](0011-explicit-runtime-type-mapping.md) | Explicit primitive runtime type mapping | Accepted for selected-library metadata mapping |
| [0012](0012-interleaved-reachability-scheduling.md) | Interleave ordinary discovery and dispatch | Accepted for bounded scheduling |
