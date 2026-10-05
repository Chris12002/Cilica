# ADR 0113: Argument exception payloads and selected resources

Status: Accepted

## Decision

The explicit integer-object profile authenticates selected ArgumentNullException,
ArgumentOutOfRangeException, InvalidOperationException and NotSupportedException
layouts and binds their invariant resource kinds to exact image/type identities.
ArgumentException already has a selected runtime identity; the new argument-base
fact must match that identity. Detached resource facts are bounded, distinct and
cannot replace the base or numeric runtime-fault identities.

Supported construction includes parameterless and string forms for these types,
and the two-string argument forms with their actual parameter/message order.
Selected ArgumentException.ParamName has a separate exact intrinsic capability;
Message reads include its override and the selected range-exception override.
Inner-exception and actual-value constructors, other members, and custom
forwarding into the new resources remain diagnostics. Legacy CIL scope remains
unchanged. This is a bounded intrinsic subset, not a port of the full exception
library or localized resources.

Native v19 adds a traced parameter-name reference to the private exception
payload. Nonempty names are appended to Message with raw UTF-16 copying in one
bounded four-segment allocation. Inputs remain retained by the rooted exception
across collection; failures preserve pending-exception origin and parameters.
Repeated formatted messages retain .NET's distinct-result behavior. Null/empty
names, supplied messages and selected default/null-message resources preserve
their existing identity behavior. The bounded resource cache now permits the
nine previous resources plus four additional defaults.

## Evidence and review

Five C# probes match .NET in 1,470 repeated O0/O2 sanitizer results, covering
constructor forms, parameter identity, embedded zero/surrogates, repeated message
reads and catches. Dedicated native probes verify parameter-only reachability,
allocation failure, forged references and all thirteen cached resources. The
oracle exposed a null-message fallback difference for InvalidOperationException;
it was corrected. The O0 harness was split to preserve the existing 2 KiB stack
check. Focused regression results are recorded in the milestone log.

The private kernel now reaches a value-returning interface signature in the
handler registry. Native kernel transcript equivalence remains pending.
