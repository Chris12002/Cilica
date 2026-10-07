# ADR AC0144: Retained-stack sealed receiver proof

Recorded 2026-10-07 for AC8.39.

## Evidence and decision

The AC8.38 diagnostic copy reaches SemanticFingerprint.Create's frozen sequence:
new StringBuilder; dup; load nodes; call AppendNodes; callvirt Object.ToString.
The receiver is still a sealed StringBuilder, but the old immediate-producer
proof sees a void method call and attempts the unsupported Object body.

Track the selected top stack slot backwards over a bounded straight-line window.
Known instructions supply stack effects; dup maps both top outputs to their
single input. Calls account for explicit arguments, instance receiver and return
value. Only the existing local/argument/field/constructor/call-result identities
supply the candidate type. Nops, pops, stores and argument producers can preserve
an older receiver. Unknown operations or 256 instructions/stack slots stop proof.
Cached predecessor maps end at branches and try/handler/filter boundaries.

This is discovery evidence, not trusted executable typing. Existing translation
still requires the actual SSA receiver to match the selected method, preserves
callvirt null checks and dispatch, and validates fault/rooting order. Original
public/externally-visible virtual declaration checks and exact slot resolution
remain unchanged. No application body is executed, rewritten or specialized by
name. Integer CIL v61; no other profile or production budget changes.

## Validation and review

Roslyn in the authored fixture uses a local rather than the frozen stack form.
The test therefore copies only that authored fixture and mechanically replaces
local-zero store/load instructions with equivalent retained-stack nop/dup forms;
it asserts a straight-line body and final callvirt. Header max-stack is updated
for the test copy. The untouched authored C# runs as the .NET oracle; the copied
IL is compiled to native. No frozen assembly is edited and no arbitrary assembly
is loaded for execution in the compiler process.

Four probes cover retained/duplicated constructor and call-result receivers,
ignored call results, repeated copies, allocating callbacks, exact UTF-16 builder
text, null receivers and callback/ToString fault order. Raw negative IL checks
unrelated stack objects, branch entry and the scan bound. Existing access and
Object-alias negatives remain. Review checks reverse stack arithmetic, candidate
versus actual typing, metadata budgets, access, and GC roots across callbacks.
All 45 selected checks pass: exact stack-form fixtures, original sealed receivers,
StringBuilder, virtual value signatures, constrained reference calls and the public
baseline. Four new probes supply 1,176 repeated native/source-oracle comparisons
at O0/O2 with ASan/UBSan, forced GC, exact cleanup and unchanged native frame/
subprocess caps. The original local-based fixture also passed but did not test
the frozen pattern; the guarded authored-IL rewrite is the authoritative evidence.
Public baseline stays 46 controls/one gap. Local main is still ancestor 48ecf14.
Evidence: artifacts/alchemy-stack-receiver/{tests-exact,regressions}.log. Full
fingerprint and replay execution remain unverified pending the closure refresh.
