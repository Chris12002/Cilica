# ADR AC0004: Nominal integer and enum boxes

Accepted 2026-10-06 for AC1b.2 in the integer managed-object profile.

## Decision

Add box-only scalar descriptors for the selected Boolean, Char, SByte, Byte,
Int16, UInt16, Int32, UInt32, Int64 and UInt64 definitions, and already admitted
public nongeneric Int32 enums. Preserve each exact assembly/type identity while
using one runtime-owned Int32 or Int64 carrier slot. Resolve selected primitive
identities through metadata; matching a spelling does not select scalar semantics.

ValueTypeLayout and ValueTypePlan carry an optional scalar-box code. The slot has
an empty FieldDef path because it belongs to the runtime, not a fabricated managed
field. Such descriptors cannot be used as inline values, nested/heap fields,
value-array elements or inline interface arguments. The existing layout, type,
copy-slot and metadata limits apply. Detached validators reject unsupported codes,
managed fields on scalar descriptors, and attempts to use them as inline storage.

BoxValue/unbox.any accept the scalar carrier for those descriptors. Scalar lowering
normalizes narrow signed/unsigned values, then reuses existing box packets, checked
owners, roots and copy locations. Raw scalar unbox remains a diagnostic until
width-aware borrowed locations have their own evidence. No managed primitive
fields, allocator, intrinsic grant or public embedding ABI are added. Reference
binding plans add exact mappings for Boolean, Char and the four small integer types.

## Identity and unboxing compatibility

A local .NET oracle compared every admitted primitive plus two Int32 enums.
Ordinary unboxing admits Int32 and any admitted Int32-backed enum interchangeably;
UInt32, Boolean, Char and the other primitive types remain distinct. Nullable
unboxing requires the exact underlying nominal identity, including the enum.
Distinct boxes retain distinct object identities and GetType retains their actual
primitive/enum identity regardless of an allowed unbox conversion.

Emit bounded descriptor compatibility groups from scalar codes. Reuse the existing
nullable diagnostic flag to request exact checking for nullable helpers; ordinary
unbox uses compatible checking. After the check, form locations through the actual
box's descriptor/accessor. Casting an enum box's C++ storage to a different box
struct would violate aliasing rules even when both contain the same integer bits.
The actual-descriptor path avoids that cast and retains the owner across collection.

## Explicit limits

Scalar/nullable isinst and castclass were deferred by this slice and are now
extended by [ADR AC0005](AC0005-boxed-type-tests.md).
The bounded core does not yet declare all framework interfaces on primitives/enums.
A closure containing scalar boxes and interface casts/tests or interface dispatch
is conservatively rejected; an omitted contract must not silently become a false
interface test. This applies even when a particular interface operation appears
unrelated to a box. Completing those contracts or a narrower use proof is follow-on
work. Existing boxed virtual/interface dispatch restrictions remain in force.

Non-Int32 and nonpublic enums, floating/native-sized values, raw scalar/nullable
unbox, nullable key semantics and full primitive library APIs remain outside this
slice. Full AC1b and full saved-game replay are not complete.

## Evidence and review

The scalar suite has 18 O0/O2 native probes (5,292 repeated comparisons); the
nullable suite has 31 (9,114), all against .NET on macOS ARM64 with forced GC and
ASan/UBSan. Coverage includes every admitted width, full 64-bit preservation,
null/default/present states, fresh identity, enum compatibility versus strict
nullable checks, exact wrong-type messages, object arrays, generic calls and
raw SByte boxing without a preceding conversion. Existing nullable heap-budget
and recovery checks also pass.

146 focused regression checks pass across layouts, boxes, core admission,
exceptions/embedding and the unchanged 37-gap rejection baseline. Initial direct
Byte boxing identified its missing reference mapping. One large nullable native
compile hit the existing 30-second timeout; splitting the suite into batches of
at most 16 probes passed without increasing that limit or reducing coverage.
Review checked runtime-owned layout isolation, normalization, exact/compatible
checks, actual-descriptor access, collection roots and interface omissions.

Value layout v3, boxed IR v5, scalar boxed IR v6, boxed payload runtime v3 and
native v39 record the extension. No hosted work, private replay rerun or additional
platform validation occurred. Historical Alchemy observations remain unchanged.
