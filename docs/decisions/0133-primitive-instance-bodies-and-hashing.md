# ADR 0133: Primitive instance bodies and integer hashing

Status: Accepted

## Decision

Admit ordinary managed instance bodies declared by the exact selected Int32,
UInt32, Int64 and UInt64 definitions. A primitive receiver is a checked scalar
borrow; it is not an inline struct layout, boxed reference or platform-private
field. Discovery requires public sealed sequential nongeneric values without
initializers, packing or stack-only markers. The compiler-emitted one-byte size
marker for an empty primitive declaration is permitted; storage remains the
selected 32/64-bit scalar, never that declaration marker. Existing managed-body,
access, resource and signature checks remain in force.

The detached PrimitiveInstance binding carries the selected TypeSource and
primitive code. Validation checks core-image identity, declaration name/token,
code/width, receiver signature, direct-call flags and consistent identities.
After validation, scalar lowering uses the existing static native calling
convention with its explicit checked receiver. Return borrows, constructors,
class dispatch, primitive constrained calls and narrow receivers remain out of
scope. The object operations IR profile advances to v9; native v20 is unchanged.

Implement the four GetHashCode overrides in owned managed source. Int32/UInt32
return their bits; Int64/UInt64 XOR their two 32-bit halves. C# emits ldind.u4 for
the unsigned receiver; it now reads the existing checked 32-bit storage without
changing its bit pattern. No new runtime intrinsic or grant is needed.

## Evidence and review

Five actual mapped-core probes match .NET in 1,470 repeated native O0/O2 ASan/UBSan
results, including 294 new integer-hash results over zero, signed boundaries and
mixed wide halves. Existing frame, borrow, collection and ownership checks run.
The composed regression run passes 40 checks covering value/constrained calls,
wide object composition, provider identity and core mapping. Detached-IR tests
reject missing/forged identities, mismatched widths/kinds and virtual slots.
Review added duplicate-code identity rejection and tightened primitive metadata
shape checks. All 11 follow-up targeted tests pass (41 distinct checks overall), including the
compiler-emitted one-byte empty-primitive size marker.

Hashing keeps its algorithm in managed source. Numeric ToString methods,
format-provider behavior, collections, primitive boxing and native kernel
transcripts remain later work. No hosted work or production approval is added.
