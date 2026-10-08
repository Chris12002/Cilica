# Checked native conversions confined to array indices

Recorded 2026-10-08 for compatibility batch 008.

The pinned CRC table pattern combines a variable Int32 stride and UInt32 index.
Roslyn produces Int64 arithmetic followed by conv.ovf.i before stelem/ldelem. The
independent WideIndex reduction was the prior CIL2001 boundary; it now executes.

Introduce CheckedNativeIndex(operand, unsignedSource) in the object integer profile.
Its semantics are a checked conversion to signed native integer at the emitted
target's intptr width, followed by bounded array-index encoding. Both source
widths and conv.ovf.i.un are preserved. The generated C++ uses the existing guarded
integer conversion with sizeof(intptr_t)*8 and rejects targets other than 32/64 bits.
The compiler's own process width is not used to determine target behavior.

After native overflow checks, values outside [0,Int32.MaxValue] become the existing
invalid -1 index. This cannot make an invalid index valid: every admitted vector
length is a nonnegative Int32. Canonicalization does not throw. Overflow remains at
the IL conversion site, before store-value evaluation; null/bounds/type-store
faults remain at the array operation, after evaluation. A 64-bit index such as
2^32+1 must not truncate to 1. No new array layout, quota, ownership or grant.

The frontend tracks these encoded indices as opaque pending values. Only one
array load/store/address consumer in the same block may pop them. Arithmetic,
conversion, locals, dup, calls, returns and cross-block transfers stay diagnosed;
unused conversions cannot silently disappear through throw/leave stack clearing.
Native-int signatures/locals and unchecked native conversions remain unsupported.
Admitted value/reference element addresses reuse existing borrow checks; scalar
element addresses remain unsupported. IR packet validation checks operand/result
widths and explicit overflow fault edges before and after scalar lowering.

CIL 86, object IR 56 and native 78 record this bounded extension. The existing emitter
helper preserves source signedness without signed C++ overflow. GC roots and fault
origins follow the existing expression/array paths. Review checked stack use,
transfer/escape rejection, exception preparation and preservation across calls,
source-width casts, signed minimums, .un maxima and invalid-index canonicalization.

Seven independent C# probes, raw Int32 IL, a static HashAlgorithm-owner reduction
and promoted WideIndex compare normal .NET with native O0/O2. A labelled test forces
only the emitted conversion width to 32 and compares its store-fault transcript
against independent checked Int32/.NET arithmetic. This exercises 32-bit width
semantics on the 64-bit host, not an actual 32-bit ABI. No external candidate code ran.
See ../compatibility-candidates/batch-008-results.md for counts and limitations.
