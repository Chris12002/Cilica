# ADR 0165: Shared protected-method binding ledger

Status: Accepted without raising metadata limits

## Decision

Allow the protected-method reader to use the caller's metadata ledger. Each read
uses a bounded view capped by both the requested per-read limit and remaining shared
work. The view charges its parent once and shares only complete member-name indexes.
Read snapshots and call-shape work are included in MetadataMembersRead, which reports
cost already charged when a shared ledger is supplied. The compiler avoids charging
that result a second time. Independent reads retain a fresh ledger.

Return successful resolved call declarations in the protected input, keyed by operand
token within its one exact closed method. Discovery seeds its source-image/token/
closed-caller cache from those facts, then performs ordinary instruction-level access,
intrinsic, argument and operation checks. Failed reads/bindings are not seeded.
No global cache, semantic bypass or increased limit is introduced.

## Evidence and review

A regression reads two bodies sharing a member owner with 64 unrelated methods.
The shared ledger pays for one complete name scan and records exactly each read's
charge; a fresh small ledger cannot reuse it. Tight per-read limits and exhausted
parents still fail. Existing exact metadata boundaries, closed caller separation,
method limits, protected input, mapped admission and native exception/core/generic
checks pass: 70 focused/composed tests, plus a clean full solution build.

Review checked per-read caps, parent charging, completed index publication, closed
caller cache keys, unchanged authorization and no double charging. The kernel passes
the prior work limit and reaches PlayerId value-array allocation in Adapter.Prepare
IL_008d. Native transcript equivalence and implementation/filter closure remain
pending.
