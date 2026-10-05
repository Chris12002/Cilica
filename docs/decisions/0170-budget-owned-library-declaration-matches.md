# ADR 0170: Budget-owned library method declaration matches

Accepted 2026-10-06; milestone 6c.55.

The kernel again reached the 65,536-unit metadata ledger after value arrays were
admitted. A temporary local work trace identified repeated mapped declaration
signature projection and generic parameter comparison, including 11,843 units in
owner-arity inspection. The tracing instrumentation was removed after measurement.
Do not increase the metadata limit to pay for these repeated immutable checks.

A caller-owned MemberResolutionBudget now retains successfully matched library
method declarations, keyed by the exact binding-plan object, resolver-owned source
image and MethodDef. The cached result contains the canonical owner, implementation
MethodDef and projected open declaration signature. Publish only after complete
candidate, ambiguity, attribute and generic-obligation validation. Every lookup
still consumes work. Failed or partial matching never publishes a result.

Each call still resolves its source in the closed caller, projects owner/method
arguments and substitutes those arguments into the cached declaration. Executable
constraint validation, access decisions and intrinsic authorization remain separate.
Bounded child ledger views share the cache; unrelated budgets and plans do not.
Entries are bounded by charged work and live only with the caller's ledger.

Validation: 121 focused binding, constraint, budget, protected-reader and library
admission checks pass; the solution builds cleanly. A regression alternates closed
Int32/Int64 owner and method arguments, verifies cheaper nonzero warm work, rejects
fresh/partial short ledgers and prevents reuse across a different binding plan.
Review checked cache identity, immutable declaration facts, successful publication,
closed substitution, authorization separation and shared-ledger accounting.

The unchanged kernel/core gate now passes metadata exhaustion and reports the
existing boxed-interface-dispatch restriction in ResolutionHandlerRegistry.Get
IL_0016 (`artifacts/milestone6-harness/gate-mapped-6c55.json`). Native transcript
comparison and milestone closure remain pending.
