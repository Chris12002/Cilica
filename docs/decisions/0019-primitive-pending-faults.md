# ADR 0019: Primitive pending faults and Int32 division

Status: accepted for the bounded primitive profile. Recorded 2026-10-01.

## Context

Division is the first throwing operation in the executable Int32 subset.
Returning a placeholder as an ordinary value would allow subsequent stores,
branches and calls to run after a managed fault. [ADR 0004](0004-exception-handling.md)
proposes explicit propagation through a pending exception in an execution context.
Implement its primitive control-flow foundation before managed allocation and
handler lowering; do not claim the complete exception runtime yet.

Signed integer division truncates toward zero and zero divisors raise
DivideByZeroException. Signed MinValue/-1 is unrepresentable; the CIL reference
describes an ArithmeticException and platform-specific OverflowException behaviour.
The selected Cilica/.NET profile uses an Overflow fault for that case, independently
observed on the recorded local runtime. Unsigned division interprets the same
Int32 stack bits as UInt32 and has no signed overflow case.
Source: [OpCodes.Div](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.div?view=net-10.0),
[OpCodes.Div_Un](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.div_un?view=net-10.0).

## Decision

Append signed/unsigned division operations to the Int32 enum. Translate `div` and
`div.un` with ordered right-then-left pops and Int32 results. Definitions for
divisions and calls have an explicit `MethodExit` exceptional target. Reject
missing/unknown targets and targets on pure definitions before emission. Every
call carries an edge even when later analysis proves its callee pure.

Use `int32-pending-fault-v1` for the updated IR/API shape. Retain call-free
`Translate` and complete-closure `TranslateProgram`. This early IR/ABI is evolving;
existing caller-built call nodes must now supply their failure edge.

Each generated internal function receives the same private `Context&` before its
Int32 arguments and returns an ordinary Int32. The context contains a primitive
pending tag plus the originating image SHA-256, MethodDef and IL offset. A
reverse-call worklist propagates may-fault effects from divisions through callers,
including recursive cycles. It runs only after full IR validation and stays within
existing program budgets. It conservatively treats conditional divisions as
throwing and omits checks only for proven pure callees.

Guard zero divisors and signed MinValue/-1 before any native division. Signed
operands widen to Int64; the quotient converts to UInt32 by defined modular
conversion. Unsigned operands divide as UInt64 and narrow to UInt32. A failing
guard sets the origin/tag and returns immediately. After a may-fault call, test
pending immediately and return before any later operation, store or edge copy.
Callers preserve the originating fault; they do not replace it with their own
call location. No C++ exception or RTTI mechanism is used.

Every program exposes a distinct `_try` C entry taking four output pointers
followed by its Int32 inputs, and returning a UInt32 status:

| Status | Meaning |
| --- | --- |
| 0 | Success; result written, origin numbers zero and image null |
| 1 | Divide-by-zero fault; result unchanged |
| 2 | Overflow fault; result unchanged |
| 3 | At least one null output pointer; no execution or writes |

Outputs are Int32 result, UInt32 MethodDef, UInt32 IL offset and `const char*`
image hash. They require valid, disjoint storage; invalid non-null pointers are
outside the caller contract. Origin strings borrow the generated library lifetime.
A fresh automatic context per invocation prevents stale status and is private
to that call. No context or pending ownership crosses the boundary. Proven pure
programs also retain their original value-only entry. A may-fault program has no
value-only C wrapper. `CppArtifact.EntryPoint` selects the correct entry;
`FaultEntryPoint` and `MayThrow` expose the convention explicitly. A distinct
symbol prevents calling a throwing program through the old value-only signature.

Managed exception objects, type identity, messages, stack traces, handlers,
finally/fault, rethrow and filters remain unsupported. This primitive context
has no managed references/allocations and hence no rooting obligations yet. The
later runtime must replace tags with rooted managed exceptions while preserving
the explicit failure edges. ADR 0004 remains proposed for that larger design;
the CoreLib/Alchemy general-filter gate is unchanged. Remainder and checked
arithmetic remain errors in this slice.

## Evidence and review

Reduced tests check both division operations, operand direction and IL offset,
underflow, missing/unknown failure edges, forged pure edges, and effect propagation
through throwing and pure recursive cycles. Existing call-budget tests now supply
the required edges without weakening aggregate limits.

Ten authored entries yield 3,630 native outcomes per configuration: signed and
unsigned division, transitive and cross-assembly calls, opposite competing-fault
orders, loops, bounded recursion, conditional avoidance and a pure status entry.
The independent .NET oracle executes only authored fixture code and catches the
two expected exception kinds. The compiler treats all input images as data.
Expected origin hashes/tokens are computed independently, with division offset 2.
Faults preserve a sentinel result; later success clears stale origin/status in the
same native process. All four null-output variants reject before dividing by zero
or changing outputs. Batches retain existing timeout/output bounds.

Both `-O0` and `-O2` builds use `-fno-exceptions -fno-rtti -Wall -Wextra -Werror`;
the optimized build enables UBSan. The prior 16,252 pure native comparisons per
configuration remain intact. Review covers guard order, widening/narrowing,
immediate propagation, recursive analysis, source injection, namespace/private
linkage, ABI pointer/string lifetimes and unchanged program/process bounds.
The exact local toolchain is recorded in the compatibility manifest.

Final validation: solution build with zero warnings/errors; all 547 local tests
passed in 16.7 seconds. Review found no remaining issue within the declared
primitive subset. Local main and cached origin/main are ancestors of the working
branch; the remote still advertises no heads, so no upstream integration was
available. No push or hosted work ran.

## Alternatives and next work

A composite result per internal call would diverge from ADR 0004 and change every
return convention. C++ exceptions would introduce an unvalidated target-dependent
mechanism. Silently returning zero through the existing C entry would lose faults.
All were rejected. Primitive tags allow propagation tests now; they do not stand
in for the complete managed exception semantics.

This completes sub-slice 2e-b. Add checked Int32 add/subtract/multiply next using
the same explicit failure path, then settle remainder semantics for the selected
profile. Full exception handlers need a separate lowering and identity design.
