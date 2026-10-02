# ADR 0031: Native inherited fields and constructor chains

Status: accepted for the bounded object operation and native profiles.
Recorded 2026-10-02.

## Context

ADR 0030 established class identity and ancestor assignments while rejecting
inherited native execution. Reinterpreting a derived allocation as the old base
struct would not establish a legal C++ object or correct offsets. Constructors
also need to retain the dynamic type and roots while each base body executes.
C# field initializers may execute before the immediate-base call, so requiring
that call as the first operation rejects ordinary authored constructors.

## Decision

1. `object-frames-native-v3` emits one standard-layout allocation per concrete
   class, with one Object header and all ancestor fields followed by declared
   fields. There is no C++ inheritance or RTTI. Declaration identities remain
   unchanged in the detached catalog. Inherited member symbols include their
   declaring image/type identity; hidden fields and equal tokens in different
   images remain separate storage.

2. Compose fields iteratively, base first. A declaration's logical index stays
   the same in all descendants. Each concrete descriptor records its immediate
   base, measured `sizeof`/`alignof`, all field `offsetof` values, the complete
   inherited reference map, and typed destruction. A descriptor callback
   recovers the actual allocated C++ type and returns the address of its typed
   member through a bounded table of individual accessors; offsets never
   manufacture pointers. Execution indexes that table directly; diagnostic
   offset lookup uses a shared binary search over measured field offsets.
   Field access then uses the declared storage type. Base methods never cast a
   derived allocation into a separately generated base struct. Check heap
   membership before descriptor access and ancestry before field access.

3. Cap physical field copies across every included concrete layout at 65,536,
   separately from the existing 256-type/4,096-declaration catalog limits.
   Reject excessive native composition before roots/source generation. This
   bounds inherited storage declarations and field/reference offset tables.
   Detached catalogs retain their previous declaration budgets.

4. `object-operations-ir-v2` requires one immediate-base constructor call on
   the direct parameter-zero receiver, with evaluated assignable arguments.
   Reject skipped/repeated bases, same-owner constructor delegation and
   receiver aliases. Before the base call, allow independent argument work and
   writes to this class's own declared fields, including readonly initializers.
   Reject reads, comparisons, local aliases, escapes or inherited-field writes
   involving the unfinished receiver. Constructors remain single-block; the
   selected Object constructor remains the explicit trivial nop/ret body.
   Existing public/same-owner access rules remain; family/internal access,
   abstract classes and implicit inherited MemberRef owners need later work.

5. Allocate and publish the most-derived object before entering any constructor.
   Every base receives that same reference, retaining its dynamic descriptor.
   Preserve initializer, argument and base-body exception order. Base bodies
   use the same frame arena, pending-fault path and root cleanup as other calls.
   No special native recursion, hidden headers or constructor-only arena exists.
   Retain depth 64, execution/object budgets and the packed Int32 ABI.

6. Keep each field operation in its own native step and descriptor lookup in a
   non-inlined helper. The first implementation repeated arena expressions in
   lookup code and produced O0 sanitizer frames up to 2,976 bytes. Lookup
   extraction alone left a two-operation step at 2,112 bytes; isolation restores
   the existing 2 KiB gate. Other statements keep the two-statement baseline.
   A single wide accessor switch also produced roughly 64 KiB O0 frames at the
   4,096-field limit. Individual typed accessors keep that growth off the native
   stack; their count is bounded by the physical field-copy limit.
   This is necessary stack containment for added semantics, not a throughput
   optimization or evidence to resume ADR 0029's larger batching experiment.

## Validation and follow-up

An authored three-level C# hierarchy tests private base storage, readonly and
hidden reference fields, base-typed locals/arguments/returns, inherited calls,
identity and zeroed fields. Native O0/O2 differential cases check results,
fault kinds and exact IR origins through private helpers and the packed export.
Allocation observers inspect the derived receiver and inherited reference paths
while pre-base initializers and base bodies allocate. Native cases cover
allocation at every stage, exact/short execution budgets, initializer-before-
argument fault order, reuse after failure, foreign/invalid/wrong-type inputs and
typed destruction. A separate cross-image fixture gives base/derived fields
equal names and FieldDef tokens while checking their distinct native values.
Its expanded variant exercises 4,096 field accessors at O0/O2 with the same
sanitizer/frame warning gate.

Review rejected byte-pointer offset addressing in favor of typed member
expressions: the C++20 object-representation wording has a known arithmetic gap
documented by [P1839R6](https://www.open-std.org/jtc1/sc22/wg21/docs/papers/2024/p1839r6.html).
Measured offsets remain descriptor metadata and accessor selector keys.

Metadata/IR regressions cover missing/skipped/repeated bases, null/aliased
receivers, premature reads/escapes/comparisons, inherited readonly writes,
constructor control flow and exact/excess physical field budgets. Review moved
constructor diagnostic formatting onto failure paths; a long-name, 8,192-value
valid prefix has a generous allocation bound to prevent diagnostic amplification.

Casts/type tests and verified dispatch follow this executable layout. CIL joins
still conservatively widen different class references to Object; declared locals
and detached joins support ancestor types. Actual collection, broader access,
constructor delegation and other native targets remain unimplemented/unverified.

Final validation: build succeeds with zero warnings/errors. All 526 targeted
native, object, reference-IR, C++ and Int32 tests pass in 43.3 seconds, including
both optimization levels, the 4,096-field accessor regressions, native-stack
checks and existing arena lifetime/recursion cases. The 21 focused hierarchy/
constructor cases also pass after the final accessor change. Review covered
declaration/index identity, C++ subobject lifetime/addressing, unfinished receiver
flow, resource bounds, rooting and all fault exits. Local main and cached
origin/main remain ancestors; no push, hosted work, extra agent or runtime
service was started.
