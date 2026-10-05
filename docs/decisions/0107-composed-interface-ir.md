# ADR 0107: Interface dispatch in composed value IR

Status: Accepted

## Context and decision

The value/delegate/exception pipeline must preserve interface plans before closed
CIL discovery can use exact targets. Add an optional `InterfaceDispatch` plan to
`DelegateValueIrProgram`, admitted with the explicit integer-object catalog.
Validate it against executable signatures and complete reachable bodies through
the existing object rules, then retain it through scalar lowering.

Interface declarations and mapped methods currently have scalar/reference
signatures. Struct and borrow signatures remain rejected by plan validation.
Their identities and signatures therefore survive value scalar lowering without
an adapter. Existing native dispatch, checked receiver ownership, frames, roots
and fault routing are reused. Catalog agreement now includes integer capability
and delegate facts; closed method identities must match their owner specialization.

## Evidence and limits

Four authored compositions of independently translated closures match .NET in
1,176 repeated O0/O2 ASan/UBSan results. They combine wide inline copies, boxing,
borrows, allocating interface reference returns, inherited slots and managed
catches. A null-interface probe verifies that its division argument faults before
the null receiver check. Every probe forces collection and checks exact/short
frame budgets and cleanup. Malformed plan tests cover catalog, target and identity
disagreement. All 75 distinct focused checks pass; review found no remaining issue.

These are explicit IR compositions. Automatic closed interface CIL discovery,
variance-only dispatch, struct signatures and array helpers remain later gates.
