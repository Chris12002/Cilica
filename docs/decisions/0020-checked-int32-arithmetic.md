# ADR 0020: Checked Int32 arithmetic

Status: accepted for the bounded primitive profile. Recorded 2026-10-01.

## Context

[ADR 0019](0019-primitive-pending-faults.md) supplies explicit failure exits,
pending primitive faults, conservative call effects and a native status entry.
Checked arithmetic must detect an unrepresentable result before narrowing it,
and preserve the originating operation and evaluation order through callers.
Signed native overflow cannot be used to detect managed overflow.

The CIL checked arithmetic forms raise OverflowException when the result does
not fit the selected signed or unsigned range. `.un` interprets the Int32 operand
bits as unsigned; it does not require changing the public Int32 signature.
Sources: [OpCodes.Add_Ovf](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.add_ovf?view=net-10.0),
[OpCodes.Mul_Ovf_Un](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.mul_ovf_un?view=net-10.0),
[OpCodes.Sub_Ovf_Un](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.sub_ovf_un?view=net-10.0).

## Decision

Append six operations to the Int32 enum, preserving earlier values. Translate
`add.ovf`, `sub.ovf`, `mul.ovf` and their `.un` forms with ordered right-then-left
pops, Int32 results and explicit method-exit failure edges. Keep the established
`int32-pending-fault-v1` data shape and native status convention; this is an
additive capability within an experimental API.

Share the checked/signed/may-fault classification between translation, public IR
validation and the reverse-call effect analysis. Validate known operation kinds
and earlier operands independently. Each new throwing definition must carry its
method-exit edge. Recursive callers inherit the effect, and pure operations
remain pure. Existing aggregate program and process budgets are unchanged.

Compute one wider temporary, then check and narrow:

| Operations | Intermediate | Valid result range |
| --- | --- | --- |
| Signed add/subtract/multiply | Int64 after reinterpreting operand bits as Int32 | -2^31 through 2^31-1 |
| Unsigned add/multiply | UInt64 after widening UInt32 bit patterns | 0 through 2^32-1 |
| Unsigned subtract | Int64 after widening UInt32 bit patterns | 0 through 2^32-1 |

All signed Int32 sums, differences and products fit Int64; the largest positive
product is 2^62. Full UInt32 products can exceed Int64Max but fit UInt64, with a
maximum of (2^32-1)^2. Unsigned differences fit Int64, which makes negative
results directly testable. This avoids native signed overflow and avoids needing
to interpret wrapped unsigned subtraction as underflow.

On an out-of-range result, record the existing Overflow tag and current
image/MethodDef/IL offset, then exit immediately. A successful result converts
to UInt32 only after the check. Negative signed results use defined modular
conversion; successful unsigned results above Int32Max preserve their bit
patterns. Existing call checks, fresh invocation context, preserved result output
on fault and borrowed origin-string lifetime apply without ABI changes.

No managed allocation, references or new ownership cross the boundary. Full
exception objects, identity/messages, catch/filter/finally and rooted propagation
remain planned. Remainder, unary negation and conversions are unsupported.
The CoreLib/Alchemy general-filter gate remains unresolved.

## Evidence and review

Reduced fixtures verify all six instruction mappings, operand order/IL offset,
zero/one-operand underflow, missing/unknown failure edges and effects through
recursive cycles. Pure-edge rejection and existing public-IR/resource checks
remain intact. Previous unsupported checked-arithmetic fixtures now test unary
negation; supported operations have their own positive and negative cases.

The existing native fault harness expands from ten to 25 entries and checks
9,325 outcomes per configuration. Six direct authored checked operations compare
with .NET, alongside helpers, loops, recursion, conditional avoidance, cross-assembly
calls and mixed division/arithmetic. Unsigned fixture casts explicitly preserve
bits outside the checked arithmetic, avoiding unsupported checked conversions.
The compiler never executes input assemblies.

Inputs include 100 boundary combinations, 256 seeded tuples and 17 ordered/boundary
regressions. Products straddle signed 46340/46341 and unsigned 65535/65536 limits;
full UInt32 products exceed Int64Max. Negative inputs distinguish signed values
from unsigned bits. Competing faults test opposite orders, including two Overflow
tags at different methods. Signed and unsigned compound expressions verify first
fault offsets 2/4 within one method, with independently authored reference
operations identifying the expected origin. Success after faults, sentinel result
preservation and all four null-output checks remain. Fifty-tuple batches retain
the existing capture bound.

Both `-O0` and `-O2` use warning-as-error builds with C++ exceptions/RTTI disabled;
optimized execution retains UBSan. The prior pure harness still compares 16,252
results per configuration. Review checks intermediate widths, literal ranges,
check-before-narrowing, unique temporaries, signed/unsigned bit interpretation,
first-fault provenance, shared effect classification and unchanged ownership/budgets.
No remaining issue was found within the declared subset.

Final validation: zero build warnings/errors; all 583 local tests passed in
12.0 seconds. The exact local toolchain remains in the compatibility manifest.
Local main and cached origin/main are ancestors of the branch; upstream still
advertises no heads, so no integration was available. No push or hosted work ran.

## Alternatives and next work

Native Int32 arithmetic followed by a range check would already invoke undefined
behaviour for signed overflow and lose information for unsigned results. Compiler
overflow intrinsics would add a target/compiler dependency before the portable
baseline was established. Wider exact arithmetic is simpler to review for this
bounded type. Optimize only with preserved differential and sanitizer evidence.

This completes sub-slice 2e-c. Next define and execute signed/unsigned Int32
remainder, explicitly resolving zero divisors and signed MinValue/-1 for the
selected profile. Broader arithmetic widths and managed handlers need later slices.
