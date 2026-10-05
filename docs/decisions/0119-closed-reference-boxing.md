# ADR 0119: Closed reference boxing

Status: Accepted

## Decision

In the integer-object profile, box with a closed reference operand preserves
the reference and null identity without allocating. unbox.any with a reference
operand uses the existing checked class/interface/vector cast. unbox remains a
boxed-value borrow operation and rejects reference operands. Value-type boxing
retains its existing layout and allocation contract.

Interface cast failures now have descriptor identities used only as diagnostic
message keys. They are excluded from concrete descriptors, allocation and runtime
type ownership predicates. Message text remains bounded and uses the retained
runtime display name. Legacy exception profiles retain their interface-message
boundary.

## Evidence and review

Five C# probes match .NET in 1,470 repeated O0/O2 sanitizer results, checking null,
identity, class/interface/String casts, reference-vector covariance, forced
collection and exact InvalidCastException text. An IL regression rejects a
reference unbox borrow. All 105 distinct focused checks pass, including existing
boxing, memory, generic-reference and managed-exception regressions. Review found
no remaining actionable issue.

The private kernel advances to an ordinary struct instance call in PlayerId
equality; native kernel equivalence remains pending.
