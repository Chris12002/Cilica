# ADR 0021: Int32 remainder

Status: accepted for the bounded primitive profile. Recorded 2026-10-01.

## Context

Division and checked arithmetic already use the pending-fault path from
[ADR 0019](0019-primitive-pending-faults.md) and
[ADR 0020](0020-checked-int32-arithmetic.md). Remainder must preserve operand
order, signedness and faults before subsequent work, including the exceptional
signed MinValue/-1 case. A wider native calculation could otherwise return zero
and silently erase a fault in the selected managed profile.

The CIL reference describes signed remainder using truncation-toward-zero
division, with a nonzero result's sign matching the dividend. Unsigned remainder
uses unsigned operand bits. Integral zero divisors raise DivideByZeroException.
The signed reference also records platform-specific OverflowException behaviour
for MinValue rem -1; it must not be assumed safe solely because the mathematical
remainder is zero. Sources:
[OpCodes.Rem](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.rem?view=net-10.0),
[OpCodes.Rem_Un](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.rem_un?view=net-10.0).

Before lowering, an independently authored local .NET probe observed OverflowException
for signed MinValue rem -1 in unchecked C#, checked C# and raw IL. SDK 10.0.400 /
runtime 10.0.11 runs as ARM64 (`osx-arm64`). The permanent contract tests now pin
that observation. This is the selected Cilica profile's rule, not a claim about
every CLI implementation or platform. Constant-folded expressions are not the
reference: fixtures and raw methods take runtime parameters.

## Decision

Append signed and unsigned remainder to the operation enum without renumbering
earlier members. Translate `rem` and `rem.un` with ordered right-then-left pops,
Int32 results and explicit method-exit failure edges. Include both forms in the
shared may-fault classification and validated operation set. Calls, recursive
effects and existing budgets require no new mechanism.

Use the established division guard lowering for remainder:

- A zero right operand records DivideByZero and exits before native evaluation.
- Signed MinValue/-1 records Overflow and exits, in both checked and unchecked
  C# contexts. There is no separate checked remainder opcode.
- Unsigned remainder has no signed overflow guard; the same incoming bits
  represent values 2^31 and 2^32-1, whose remainder is valid.

Widen signed operand bits through Int32 to Int64 before native `%`; widen unsigned
bits to UInt64. Guarded signed operands cannot invoke native quotient overflow,
and the widened range also contains every Int32 operand. Convert the result to
UInt32 and retain its bits. Negative signed remainders use defined modular
conversion; successful unsigned remainders may have bit 31 set. The result is
derived from truncation toward zero, not a nonnegative mathematical modulo.

Keep `int32-pending-fault-v1`, its explicit failure edges, existing fault tags and
native status entry unchanged. The first fault retains image/MethodDef/IL origin
through callers and preserves the result output. No allocations, managed
references or new ownership are introduced. Managed exception objects/messages,
handlers and the CoreLib/Alchemy filter gate remain unimplemented or unresolved.
Unary negation, conversions and other primitive widths remain unsupported.

## Evidence and review

Reduced IR tests cover both instruction mappings, operand order/source offset,
zero/one-operand underflow, missing/unknown exceptional edges and recursive effect
propagation. Eighteen fixed contract cases assert expected values/faults against
independent raw-IL DynamicMethods and authored C# references. They include all
sign combinations, signed MinValue/-1, zero divisors, equal/minimum operands and
unsigned bit-pattern results above Int32Max. DynamicMethods emit only authored
loads/remainder/return instructions; compiler input bytes are never loaded or
executed to construct an oracle.

The native fault harness expands to 37 entries and 13,986 outcomes per
configuration: 100 boundary combinations, 256 seeded tuples and 22 explicit
boundary/order cases. Twelve new entries cover checked/unchecked signed and
unsigned remainder, helpers, cross-assembly calls, loops, bounded recursion,
conditional avoidance, competing checked-arithmetic faults and chained remainder.
Five new tuples exercise nonzero results in all sign combinations and a fault
at a compound expression's second operation. Independent origins include literal
offsets 2/4 and distinguish competing faults of the same kind.

The full native path continues to compare .NET outcomes and compile CLI-generated
source at `-O0` and `-O2`. Both fault builds disable C++ exceptions/RTTI and treat
warnings as errors; optimized execution retains UBSan. Sentinel output,
context reset and null-output checks remain. Fifty-tuple batches stay below the
existing output capture bound. The prior pure harness retains 16,252 results per
configuration.

Review covers zero/overflow guard order, profile choice versus wider arithmetic,
operand signedness, remainder sign, widening/narrowing, origin preservation,
shared effect/edge validation and unchanged ownership/resource limits. No remaining
issue was found in the documented subset. Final validation: zero build warnings
or errors; all 613 local tests passed in 13.5 seconds. Exact local native toolchain
evidence remains in the compatibility manifest.

Local main and cached origin/main are ancestors of the branch. Upstream still
advertises no branch heads, so no integration was available. No push or hosted
work ran.

## Alternatives and next work

Returning zero for signed MinValue/-1 would disagree with the pinned reference.
Relying on the native processor's behaviour would make the result platform
dependent and risk C++ undefined behaviour. Computing remainder with Int32
quotient/multiply/subtract would introduce extra overflow hazards. Explicit
guards and widened `%` provide a small baseline with independent evidence.

This completes sub-slice 2e-d and slice 2e for the documented Int32 subset.
Slice 2f will review integration, consolidate supported/unsupported boundaries
and provide a reproducible example. Full managed exceptions and broader types
remain later milestone work.
