# ADR 0025: Bounded native objects, ownership and phase A roots

Status: accepted for the straight-line native checkpoint. Recorded 2026-10-02.

## Context

ADR 0024 supplies validated object operations. Executing them requires target
layouts, a heap owner, explicit faults and published references before allocation.
The first checkpoint must prove these without pretending that a collector or the
stable embedding API exists. Reference control flow remains a separate slice.

## Decision

1. `ObjectCppEmitter.EmitProgram` consumes revalidated `ObjectIrProgram` and
   emits the separate `straight-line-objects-native-v1` profile. The primitive
   emitter remains Int32-only. Object entries accept/return Int32 through an
   experimental status boundary; managed references remain internal. No object
   CLI command or retained host handle API is introduced.

2. `ObjectRootAssignment.Assign` is a separate bounded phase A pass. Assign a
   distinct slot to every reference parameter, local and temporary, including
   null temporaries. Record allocations and direct/void calls as potential
   safepoints. Methods must have one block, no stack inputs, and a return.
   Reject larger/control-flow programs before returning native source. The
   native frame limit is 1,024 slots per method; existing aggregate IR bounds
   still apply. There is no liveness analysis, slot reuse or phase B claim.

3. Each generated method publishes a zero-initialized array through a frame in
   its explicit `Context`; reference arguments are copied into it before
   publication. Reference locals and temporaries alias their assigned slots.
   Publish an allocation result before entering its constructor. Assign a
   reference call result into the caller slot immediately, with no intervening
   allocation. A scope guard pops frames on every normal/fault return under
   `-fno-exceptions`. Bound active calls to 64; reaching the bound returns a
   resource fault and unwinds normally. Zero-reference methods use empty frames
   for consistent call-depth accounting.

4. Emit standard-layout C++ structs with a header at offset zero, checked by
   `static_assert`. Headers retain immutable type descriptors, instance owner,
   heap linkage and a reserved synchronization word. Int32 fields store unsigned
   bit patterns; reference fields store object pointers. Sizes, alignments and
   reference-field offsets come from `sizeof`, `alignof` and `offsetof` on the
   native target. Field maps describe heap references, separately from method
   roots. Metadata names never become C++ identifiers. Converting between the
   struct and its first header member relies on checked standard layout; delete
   through the descriptor's exact concrete-type callback.

5. An instance owns all its allocations and deletes them at teardown. Default
   per-invocation limits are 1 MiB of object storage and 4,096 allocations;
   check capacity before nothrow allocation and zero-initialize fields. The
   current heap retains objects until teardown: it does **not** collect. The C
   boundary creates a fresh heap/context for each invocation. Private native
   helpers permit focused ownership/root diagnostics, not persistent embedding.
   Descriptor constants can be shared across instances; heaps/roots cannot.

6. Check heap membership before dereferencing a supplied reference, then check
   its exact descriptor unless the signature widens to the selected Object base.
   Null remains legal for direct instance calls unless a field is dereferenced;
   nonvirtual `callvirt` checks null after its arguments have evaluated. Field
   stores preserve explicit ordered IR and validated readonly/access rules.
   Check published roots before each allocation. A private observation hook
   inspects frames/heap maps at these points; it does not run collection.
   Membership checks are currently linear, so no performance claim is made.

7. Reuse primitive arithmetic lowering, including all guards and bit conversions.
   Propagate pending status/origin without executing later operations. Statuses:
   0 success, 1 divide-by-zero, 2 overflow, 3 invalid output pointers, 4 null,
   5 allocation failure, 6 ownership/type violation, 8 call-depth limit.
   Output pointers require valid disjoint storage; null outputs return 3 without
   writes. Failed execution leaves the result unchanged. Origin text borrows the
   generated library lifetime. These are explicit fault tags, not managed
   exception objects, stack-overflow compatibility or catch/filter/finally.

## Validation and review

Authored optimized C# executes through metadata resolution, translation and
emission, then Clang C++20 at O0/O2 with ASan and UBSan, no exceptions or RTTI.
Compare normal/wrapping results, checked/division/null fault order and exact IR
origins with .NET. Observe caller/callee roots before allocations, including
reference returns, allocating constructors and children retained through fields.
Check default fields, native descriptors, distinct identity, foreign instances,
arbitrary pointers, invalid roots, byte/count limits, partial constructor failure,
frame cleanup, destruction callbacks and boundary output behaviour.

Authored raw IL independently checks direct-null-call versus nonvirtual callvirt,
null comparisons, same/distinct allocation identity, integer self-comparison and
bounded recursion. .NET executes only the authored finite oracles in the test
process; unbounded recursion is tested solely against the native resource limit.
Exact/excess frame bounds, malformed IR and valid-but-unsupported control flow
receive focused compiler API checks.

Review caught self-comparisons emitted as tautological C++ expressions under
`-Werror`; fold comparisons of the same already-evaluated temporary. Reduced
object and primitive native cases cover it. Verify roots before invoking the
private observation hook, so invalid root addresses are not exposed to observers.
The retained-heap experiment cannot prove correctness under reclamation. Precise
collection, stress at every allocation, phase B and Emscripten/other platforms
remain gates. ADRs 0001 and 0002 remain proposed for their wider experiments.
