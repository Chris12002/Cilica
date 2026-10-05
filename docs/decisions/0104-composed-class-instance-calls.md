# ADR 0104: Direct class instance calls in composed CIL

Status: accepted for slice 6c.13. Recorded 2026-10-05.

## Decision

The explicit integer-object CIL profile admits ordinary nonvirtual class instance
calls with supported value, scalar, reference and synchronous borrow signatures.
Closed generic owners retain exact specialization identities. Ordinary
`SpecialName` IL methods, including property accessors, use the same body
translation; the flag grants no runtime intrinsic permission. Virtual/interface
dispatch, class constructors with value/borrow flow and general struct instance
calls remain outside this slice.

`callvirt` on an admitted nonvirtual class method retains its receiver null check
after argument evaluation. `call` retains direct-call semantics. Value returns
still use caller-owned rooted packets, and conversion from an expression call
to the packet-writing invocation now preserves null-check and dispatch flags.
The existing native class receiver, field, borrow and ownership checks are reused.

## Validation and review

Two independent C# probes combine generic class methods, wide state, value
arguments/returns, allocating reference updates, borrowed values and property
access. All 588 repeated O0/O2 ASan/UBSan results match .NET with forced collection,
exact frame budgets and cleanup checks. Null receivers and argument exceptions
verify evaluation order. The null-call method does not read its receiver, so its
struct return specifically detects a lost callvirt check. A lowered-IR regression
also asserts that the packet invocation preserves that flag.

Review checked signature expansion, receiver position, hidden return destinations,
call flags, access checks, rooting and unchanged class-constructor/dispatch gates.
The null-check preservation gap was fixed before enabling the composed calls.
All 146 focused checks pass; no remaining actionable issue was found.
