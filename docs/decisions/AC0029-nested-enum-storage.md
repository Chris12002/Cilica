# ADR AC0029: Nested enum storage with lexical access

Accepted 2026-10-06 for bounded AC5b.3.

## Decision

An exact nongeneric Int32 enum can occupy admitted layouts regardless of nesting
visibility. Its lexical declaration chain must validate. Direct nested enum memory
operands use the existing lexical access checker: containing owners and their
nested helpers can access private nested declarations; unrelated/foreign callers
cannot. Protected nesting still requires separate family analysis. Top-level
internal rules from AC5b.1 are preserved. Generic/boxed nonpublic operands and
method/field access keep their independent existing boundaries.

## Evidence and review

Three probes pass 882 repeated O0/O2 native/.NET comparisons covering object fields,
ref mutation, copies, nested structs and arrays. Five raw memory-operand checks
cover owner/helper access and unrelated/foreign/protected rejection. Two negative
box/generic checks pass, as do 30 prior enum/lexical checks. A fixture's call from a
nested struct to its owner's private method revealed the separate existing method
access restriction: it remains an explicit negative regression. The storage probes
perform their own field mutation; no game code changed. All tests use existing
sanitizer, GC, process and frame budgets.

Review checked lexical chain authentication, same-image identity, generic arity,
unchanged nominal/width constraints and that field/method permissions are not
inferred from storage capability. No IR/native profiles or grants change.

The unchanged private state-lookup probe now reaches CIL2003, the configured
256-type combined value/reference catalog limit. The limit was not raised.
Evidence: artifacts/alchemy-nested-enums. This demonstrates deeper closure discovery,
not execution or complete replay. Public counts remain 18 gaps/29 controls.
