# ADR 0093: Int64 scalar IR and native arithmetic

Status: accepted for slice 6c.2 authored IR. Recorded 2026-10-05.

## Decision

Add a distinct `IrType.Int64`, explicit 64-bit constants/binary/not expressions,
and signed/unsigned Int32 widening and low-32-bit truncation. The new
`IntegerIrProgram` wrapper selects `integer-arithmetic-ir-v1` through
`CppEmitter.EmitIntegers`. Existing Int32/object/value profiles continue to reject
Int64 IR; no CIL translation or heap storage is admitted by this slice.

Reuse the existing method/block/local/call IR, validator, resource limits, fault
analysis and C++ emitter. Type checks require equal-width arithmetic/comparison
operands and exact call, local, edge and return types. Int64 shifts use an Int32
count, masked to six bits. The historically named `Int32Comparison` describes
the comparison operation; the validated operand width selects its interpretation.

Native temporaries, locals and block inputs store UInt64 bit patterns. Signed
interpretation uses `std::bit_cast<int64_t>`. Wrapping operations use unsigned
arithmetic. Checked signed add/subtract inspect result sign bits; checked multiply
uses unsigned magnitudes and a division-based bound before multiplication. This
needs neither C++ signed overflow nor nonstandard 128-bit integers. Arithmetic
right shift fills unsigned sign bits, guarding zero counts before computing the
fill. Division/remainder check zero and signed minimum divided by minus one before
executing a native signed operation.

The existing private fault protocol remains: status 1 for divide-by-zero, 2 for
overflow, original method/IL provenance, and unchanged result storage on fault.
May-fault analysis includes wide operations and propagates through calls. Generated
test entry signatures use the actual Int32/Int64 width; this is not the planned
public embedding ABI.

## Validation and review

Nine IR cases cover profile isolation, malformed widths and types, unknown
operations, missing/extra exceptional edges, shift counts and conversion direction.
Native tests compare 34 independent probes over 288 operand pairs at both O0/O2
with UBSan: 19 operations, three conversions, ten comparisons, wide branch/local/
edge/constant/not behavior and transitive checked-call faults. All 19,584 native
results match authored .NET arithmetic, including signed remainder overflow.

The focused IR/emitter/native-fault run passes 46 tests; the solution builds with
zero warnings/errors. Review checked unsigned overflow rules, shift count limits,
minimum-value magnitudes, fault-before-result behavior, profile isolation and
all transfers that could truncate a value. It added wide call/local/edge probes.
No actionable findings remain in this scalar IR slice. CIL lowering, checked
conversions, narrow scalars/enums, wide heap/array/borrow storage and the real
kernel trace remain subsequent work. No hosted work was used.
