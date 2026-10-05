# ADR 0137: Zero-initialized readonly static reads

Status: Accepted

## Decision

The integer-object CIL profile admits ldsfld for initonly static fields whose
implementation owner has no type or module initializer and whose declaration has
no RVA, constant, explicit or marshalled storage. Respect exact closed field
binding and the existing same-owner/public field access rule. Lower the read to
the existing scalar zero, null or inline default operation. Every inline read is
a fresh value copy; no global storage or runtime initialization is introduced.

Exclude these proven default fields from class/value instance layouts. This also
supports a value's self-typed Empty field without inventing a recursive instance
layout. Static addresses and writes, mutable fields, initialized owners and
unsupported field representations remain diagnostics. Reflection and unsafe
writes cannot mutate this storage through the admitted execution profile.

## Evidence and review

Eight focused checks and 140 related layout/value/generic regression checks pass.
Two probes supply 588 repeated native O0/O2 ASan/UBSan comparisons, including
independent copies, reference-bearing values, closed generic owners, zero-sized
values, scalar widths and null references. Initializers, mutable/private fields,
addresses, writes and literal flags are rejected; the legacy profile stays bounded.

Review checked exact field identity, access, initializer exclusion, field layout
filtering, generic substitution and copy/ownership behavior. No remaining
actionable issue was found. Existing detached/native IR is unchanged; Guid.Empty
can now use ordinary managed declarations in the next library slice. Local main
remains an ancestor; no hosted work ran.
