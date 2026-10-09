# ADR AC0249: Bound native boxed-allocation dispatch frames

Recorded 2026-10-09 for AC8.105 after 2d8842b.

## Failure and reference

The first complete EngineStep O0/ASan/UBSan compilation rejects the generated
allocate_box_storage function: its native frame is 12,448 bytes against the existing
2 KiB guard. A reduced catalog of 240 types stays inside production type limits
and reproduces a 7,744-byte frame using the previous emitter on macOS ARM64.
The same test assembly/inputs with the candidate compiler pass at O0 and O2;
baseline binary hashes and failure receipts are retained.

Unity's converted-runtime reference separates class metadata from allocation in
libil2cpp/vm/Object.cpp:262–311 (Object::New/NewAllocSpecific; SHA256
707ce8d83c943bf0e797272f1d58dd47bbda3b2565860c0a6e22bbd40c6448d5).
That shape guides the dispatch separation. Cilica keeps its existing typed C++
allocation, precise roots and independent runtime; no Unity allocator is used.

## Decision and review

Emit one small noinline allocator per boxed layout and a constant table of exact
descriptor/allocator pairs. Both known_box and allocate_box_storage compare against
that table. Allocation makes one indirect call after a successful identity match.
Unknown, null or copied descriptors return the existing failure without dereference,
allocation or changing the pending fault. The selected allocator uses its fixed
compiler-owned descriptor and the unchanged allocate<T> implementation.

Review confirms payload validation and reference publication still precede the
allocation safepoint. Typed storage, subobject provenance, alignment, deletion,
identity, quota failure, collector behavior and fault ordering are preserved.
The extra helper call does not create a managed frame or alter call-depth limits.
Module headers retain shared inline table/descriptor identity and hidden visibility.
No warning, capacity or ownership check is weakened. No outstanding findings in
this slice. Native profile becomes object-frames-native-v89; private boxed runtime
marker becomes boxed-payload-runtime-v4. IR, CIL, core grants and layouts do not change.

## Validation and remaining work

The new 240-type test allocates distinct empty/scalar boxes, copies payloads, checks
exact identities, collects all temporary objects, exercises quota failure/recovery
and rejects copied/forged/null descriptors. O0/O2, normal/forced collection,
ASan/UBSan and the 2 KiB native-frame warning all pass. A fixture-only constness
compile error was corrected before the successful run. Thirteen adjacent
NativeBoxedPayload, NativeBoxedIr and NativeModule checks pass too, for 15 distinct
passing checks. Existing cross-file tests cover references, borrows, exceptions,
static initialization and exact/insufficient execution quotas.
Receipts: artifacts/alchemy-box-allocation-table (tests.log, tests-r2.log,
baseline-tests.log, baseline-binaries.json and native hash receipts).

Full frozen EngineStep validation/emission completes in 172,757ms under unchanged
diagnostic aggregate limits. Its 67 source files plus header total 466,933,100 bytes;
module manifest SHA256
74c7820c9690d27d7f2074fdbf2bac23dc544f3d31c835978aefa828863ce880.
StateLookup totals 1,433,235 bytes, manifest
036f2dce0d9c065eef20b4143f205ef999977af3ac7c49318182466ae23a4aab.
The bounded full O0/ASan/UBSan build has cleared the previous first-unit allocator
failure and is continuing. Complete native execution, production application
capacity and the saved-game command/payload replay remain unverified.
Receipts: artifacts/alchemy-box-allocator-module-emission and
artifacts/alchemy-module-native-run/engine-O0-v2.
