# ADR AC0012: Bounded integer math and typed comparison

Accepted 2026-10-06 for AC5a.1.

## Decision

Implement System.Math.Abs for Int32/Int64 and Min/Max for Int32, UInt32, Int64 and
UInt64 in an owned managed source file. Guard signed minimum values explicitly
before negation, throwing the pinned Overflow_NegateTwosCompNum message. Ordinary
CIL comparisons preserve signed/unsigned ordering and return an input unchanged.
No native arithmetic intrinsic, host math library or new runtime operation is used.

Add each primitive's same-type CompareTo method using -1/0/1 comparisons, not
subtraction that could overflow. Declare IComparable<T> on those four primitive
metadata types so methods have the exact final/virtual/newslot flags of the pinned
reference. Existing primitive receiver lowering reads the scalar through its
managed location. This does not enable object overloads, boxing to comparer
interfaces or generic comparer execution. Math narrow/floating/decimal overloads
remain unmapped. The build recipe and explicit test binding map include Math.
No compiler profiles, intrinsic requests or public embedding ABIs change.

## Evidence and review

Eleven authored probes and the three advanced MathAbs64/MathMinMax/IntCompareTo
public probes supply 4,116 repeated O0/O2 .NET/native comparisons on macOS ARM64
with ASan/UBSan, forced GC, frame budgets and ownership checks. Cases cover extrema,
signed/unsigned ordering, both halves of 64-bit values, exact Abs exception text,
and typed comparisons. Metadata tests admit exactly ten Math overloads plus the
four typed CompareTo methods and reject remaining overloads. 35 existing core,
boxed-interface boundary and public-baseline checks pass.

An initial CompareTo probe correctly failed the strict declaration mapping check
because the methods lacked their interface flags. Adding the exact nominal
IComparable<T> declarations fixes that mismatch; no admission check was weakened.
Review checked overflow guards, unsigned CIL comparisons, operand preservation,
receiver copies and unsupported overload rejection. The ledger now has 29 gaps
and 18 controls. Required Guid helpers, comparers/sorting and unchanged private
helper execution remain for AC5a; full replay has not been rerun or claimed.
No hosted work, additional agents or services were used.
