# ADR 0032: Class casts and runtime type tests

Status: accepted for bounded concrete classes. Recorded 2026-10-02.

## Context

ADR 0031 supplies exact dynamic descriptors and immediate-base links. Reference
assignment alone permits widening to an ancestor; recovering a narrower class
or testing an object's runtime class requires explicit operations. These must
preserve identity, roots and fault order without introducing C++ inheritance or
host RTTI.

## Decision

1. Add `IrExpression.CastClass(Target, Operand)` and
   `IrExpression.IsInstance(Target, Operand)` to `object-operations-ir-v3`.
   Resolve named TypeDef/TypeRef operands in the instruction's owning image and
   cache by image/token. Include targets used only by casts/tests in the catalog;
   the existing shared metadata ledger, type/field bounds and physical-layout
   budget cover their complete ancestor/field closure. Resolution never executes
   the input assembly. Reject TypeSpecs and unsupported target shapes, including
   for statically null operands.

2. Validate an earlier reference/null operand, an exact target-typed result and
   a target in the bounded concrete catalog. Target access requires the same
   declaring owner or a top-level public class. Both operations require an
   explicit method-exit edge because either may detect invalid runtime ownership.
   The detached reference-only and primitive contracts reject these operations;
   object operation validation supplies their meaning. Reject casting/testing
   an unfinished constructor receiver before the immediate-base call; independent
   values may still be used while evaluating base arguments.

3. `object-frames-native-v4` checks heap membership before reading the operand's
   descriptor. A successful ancestry check returns the original pointer without
   changing its dynamic descriptor. Null returns null. A mismatch returns null
   for `isinst`, while `castclass` reports status 7 with the originating image,
   MethodDef and IL offset. Ownership faults remain status 6. The status flows
   through existing callers and leaves the packed boundary's result untouched.
   These rules follow the CLI behavior described by Microsoft's
   [`castclass`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.castclass)
   and [`isinst`](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.isinst)
   documentation within this deliberately smaller class profile.

4. Assign both results ordinary phase A reference slots in the existing heap
   arena. Casts/tests do not allocate, box, adjust object pointers or create
   managed exception objects. Keep each operation in its own generated step
   because its ownership/ancestry checks resemble field operations. Retain
   depth 64, current frame/root/object limits and the packed Int32 ABI. Existing
   scope guards remove roots and release storage on every return/fault path.

The new profile names identify the expanded operation/fault contracts; the
experimental packed boundary's calling convention is unchanged. Interface,
abstract, value-type, generic, array, string and TypeSpec targets, broader access
and virtual/interface dispatch remain outside this checkpoint. General C# `is`
patterns, boxing and arbitrary `as` targets are not implied by the supported CIL
class tests. Different-class stack joins still widen conservatively to Object.

## Validation and review

Two authored C# entries compare 84 results/faults per O0/O2 configuration with
.NET. They cover null, exact/ancestor/descendant and sibling/unrelated tests,
identity after narrowing, target-only inclusion, loops and allocating calls.
Competing cast, division and null faults check evaluation order and originating
method/IL offsets. Private helpers and the packed export agree, including
unchanged result storage on failure.

Native checks observe a cast-result root during later allocations, test exact
and one-byte-short execution budgets, reject foreign/invalid references, verify
null/identity results and reuse a context after invalid-cast failure. Separate
raw assemblies contain unrelated classes with identical display names and
TypeDef tokens; TypeRef targets remain distinguished by their content identity.
Both builds enforce ASan/UBSan and the existing 2 KiB native-frame warning.

Metadata/IR tests reject interface, abstract, generic, value-type, array,
constructed, inaccessible, missing and nil targets, integer operands, forged
result/target/operand identities and missing/invalid fault edges. Constructor
regressions reject casts/tests on an unfinished receiver. Review checked
descriptor dereference ordering, null short-circuiting, exact target identity,
root publication, result lifetime, fault propagation and resource accounting.
No new native ownership or C++ subobject conversion is introduced.

Local build: zero warnings/errors. All 544 targeted native/object/reference-IR/
C++/Int32 tests pass in 31.9 seconds on the recorded macOS ARM64 toolchain.
The initial focused failures were fixture setup issues (an omitted reference
directory and an ambiguous test-adapter null overload), corrected before native
execution. No hosted work, additional agents, push or service was started.
Local main and cached origin/main remain ancestors.

Verified dispatch is the next capability slice. Performance tuning stays deferred
until representative applications run. ADR 0029 now explicitly records that the
2 KiB warning is a conservative baseline, not a permanent requirement; changing
it requires measured speed gains and complete target-specific stack budgets.
