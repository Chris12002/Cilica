# ADR 0158: Reuse exact closed operand bindings

Status: Accepted without increasing metadata work limits

## Decision

Within one compiler discovery session, cache successful method and field declaration
resolution by source image, operand token and complete closed caller method identity.
The caller identity includes owner/method specialization. Preserve existing shared
nongeneric caching where no generic context exists. Apply field reuse to static as
well as instance operands.

The protected-method reader similarly caches successful call declarations by token
inside its single closed caller. Compute call/newobj receiver and result stack effects
for each instruction, rather than caching an opcode-specific shape by token.

Cache declaration facts only. Continue access checks, intrinsic authorization,
argument accounting, instruction limits and operation validation at each site.
Failed bindings are not reused. Caches end with the read/discovery session and their
sizes are bounded by admitted instructions. The metadata limit stays at 65,536;
this avoids paying repeatedly for the same successful lookup without relaxing bounds
on unique work or merging generic environments.

## Evidence and review

A metadata-only regression has 1,026 calls across two closed caller/target pairs and
repeated static field references. It exceeded the metadata limit before reuse;
afterward it retains two target bodies and two distinct static owners. Seventy
focused/composed checks pass, including closed generic execution, static initialization,
exception/control-flow input and method/resource rejection tests. The full solution
builds without warnings. Review verified complete context keys, fresh stack/access/
authorization decisions and session-scoped ownership. No native IR contract changes.

The unchanged concrete kernel passes the previous metadata gate and reaches
String.IsNullOrWhiteSpace. Native transcript equivalence and implementation/filter
closure remain required to finish milestone 6.
