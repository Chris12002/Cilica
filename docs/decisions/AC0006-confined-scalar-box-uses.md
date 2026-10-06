# ADR AC0006: Confined scalar boxes beside interface operations

Accepted 2026-10-06 for AC1b.4 in the integer managed-object profile.

## Decision

Replace the AC1b.2 whole-program scalar/interface exclusion with the existing
bounded box-use proof from ADR 0172. When the closed program has interface tests,
casts or dispatch, prove every scalar BoxValue/BoxScalars allocation is confined.
Do not rely on primitive interface ancestry: the owned declarations are still
incomplete, so absence of a declared contract cannot establish a negative test.
A scalar descriptor used only as a type-test target needs no allocation proof.

Permitted uses are identity inspection/comparison, unboxing, exact boxed type tests
and calls to ordinary parameters proven to have those same uses. Follow successful
boxed IsInstance/CastClass results as aliases, just as scalar CheckBox aliases are
already followed. An interface test/cast is never exempted by this rule. Unknown
uses, local/field/array/static stores, returned or edge-carried references,
parameter addresses, recursive parameter proofs and virtual/interface calls on
an alias fail closed. These are conservative restrictions, not claims that every
rejected managed program is intrinsically unsafe.

Run the proof again after scalar lowering; no caller-authored exemption is trusted.
Keep exact closed method/parameter summary keys, the existing 64-level recursion
cap and 1,048,576-step work budget. Existing named-value dispatch proofs remain in
force. Boxed IR v7 and scalar boxed IR v8 record the wider admission; native v40,
box layouts, generated C++, core source/bindings/grants and public ABIs are unchanged.

## Why this slice

The historical Alchemy body inventory has 1,825 rows: 1,775 readable method bodies
and 50 declarations without managed IL. The readable bodies contain 42 box, zero
unbox and zero unbox.any instructions. The inventory is a bounded incomplete
application audit with library/callback dependencies unresolved. This is enough
to deprioritize speculative raw-unbox implementation, not to prove absence from
an accepted replay closure or complete AC1b.

Scalar boxes and unrelated interface operations are a concrete composition limit.
Reusing the existing proof admits supported programs without inventing primitive
interface contracts or pretending to implement boxed interface dispatch. The
proof remains too narrow for helpers returning box references, including general
NullableBoxing<T>.Box helpers in interface-using programs. Nullable copy-unboxing
and exact type-test helpers can be proven when their references remain confined.

## Evidence and review

Thirteen native probes provide 3,822 repeated .NET comparisons at O0/O2 on macOS
ARM64, with ASan/UBSan, forced collection, frame-budget and ownership cleanup
checks. Coverage includes direct/forwarded scalar helpers, narrow/wide/enum copies,
exact and nullable type tests, nullable copies, independent interface calls/casts/
tests, descriptor-only targets, invalid casts, and allocation while a box is held
by a helper parameter.

Nine negative fixtures retain diagnostics for interface tests/casts, returned
boxes (including a nullable helper), local/array/field storage, recursive use and
parameter addresses. Detached mutations prove that isinst/castclass result aliases
cannot escape into a local at either validation stage. The valid unmodified
alias models pass before the escape is added. 129 existing interface, cast,
boxed-value, exception and public-gap regressions pass.

Review checked all scalar allocation origins, alias propagation, closed parameter
summaries, fail-closed escape paths, unchanged limits and roots, and the distinction
between type-test-only descriptors and actual boxes. A test-analyzer violation was
fixed; a nullable rejection fixture was adjusted to require an actual helper return
instead of an immediately boxed known-present value that C# simplifies.

This is bounded composition support, not full primitive interface support or full
AC1b. Raw scalar/nullable unbox, escaping box flow, primitive interface contracts/
dispatch and proof of remaining replay requirements stay open. No private replay
root was rerun, and no hosted work, services or additional agents were used.
