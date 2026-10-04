# ADR 0074: Closed generic method specialization

Status: accepted for unconstrained static methods on nongeneric owners in bounded
value programs. Recorded 2026-10-04.

`ValueTranslator.TranslateGenerics` and `ValueProgramDiscovery.ReadGenerics` opt
into `closed-generic-values-cil-v1`. Closed argument environments substitute
method signatures, locals, calls and struct TypeSpec operands before the existing
value/boxing validators and scalar lowerer run. Member matching remains in the
metadata binder and preserves declaration-level overload distinctions. A generic
call is rebound in its caller environment; a token-only cache cannot select a body
from a different instantiation.

Method source and IR identities gain a separate specialization key. The original
image hash and MethodDef token remain unchanged for provenance. A bounded canonical
argument encoding includes kind, content-scoped named definitions, argument order,
owner/method scope and vector structure. Its SHA-256 suffix distinguishes native
symbols and closure keys, even when signatures or bodies happen to be identical.
Native symbol keys are validated before emission. Open variables, invalid generic
arity and unsupported argument syntax produce diagnostics.

Discovery reaches a fixed point for recursive calls with identical closed arguments.
Every new body is charged to existing method, IL, parameter, local and metadata
budgets. Signature expansion retains the 64-level/4096-node bound; failures include
the caller chain and closed specialization identities. No runtime generic dictionary
or shared-body erasure is introduced.

This first gate supports unconstrained generic methods only. Constraint-bearing
methods, generic declaring types/layouts, primitive boxing and broader generic
operations remain diagnostics. Like the value translator it currently requires
an included inline struct; primitive-only closures need a subsequent catalog gate.
Existing translators retain their generic rejection. CLI entry points are unchanged.

Review checked closed-context binding, recursion/cache identities, native naming,
original fault provenance, expanded resource accounting and rooted parameters and
return packets. Local regressions cover Int32 and reference-bearing struct instances
of one method, nested generic calls, finite and expanding recursion, generic struct
boxing and empty struct defaults. O0/O2 ASan/UBSan probes match .NET for extreme
integers, preserve live generic arguments through allocation, unwind specialized
faults with original IL locations and leave packed output unchanged on failure.
The final selected suite passed 52 checks; the 2 KiB native stack gate remains enabled.
