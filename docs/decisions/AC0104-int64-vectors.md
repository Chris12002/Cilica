# ADR AC0104: Int64 vectors and exact array helpers

Recorded 2026-10-07 for AC7a.2.

## Evidence and design

The 39a53dd frozen-engine diagnostic reaches List<long> construction in
SettleDueInvocations IL_00f8, via FinalizeAccepted IL_00a9, after 139,611 metadata
units. StateLookup stays at eight methods/9,917 units. The reduced HashSet<long>
enumerable probe independently reaches the same vector dependency through its
empty cursor. Evidence: artifacts/alchemy-set-construction-{refresh,engine}.
Production limits are unchanged; these engine probes do not emit or execute code.

Add a distinct Int64 vector identity to the integer-object profile. CIL newarr,
ldelem.i8/stelem.i8, closed generic element operations and field/method signatures
resolve to the same identity. Logical and scalar IR require Int64 load/store
values and the existing exceptional edges. Wrong element kinds, narrow carriers,
attached nominal element IDs and the legacy profile remain rejected.

Native descriptor kind 4 selects one UInt64 slot per element, reusing the existing
wide_integers buffer, allocation accounting, zero initialization, tracing/storage
validation and cleanup used by scalarized value arrays. Checked generated accesses
use dedicated UInt64 loads/stores after ownership, descriptor, null and bounds
validation. Type names, runtime identity, empty caches and cast messages retain
System.Int64[] distinctly from Int32 and other vectors. No packed external ABI or
unmanaged pointer/borrow surface is introduced.

Exact generic array helper recipes now admit Int64 for count, indexed access,
search/mutation faults, enumeration and copying. Scalar interface compatibility
includes the pinned CLI signed/unsigned relation; explicit UInt64 array identity
and generic unsigned enumeration/copy recipes remain diagnostic. Narrow vector
dispatch and non-value element borrows remain unchanged. Existing library bodies
now execute List<long> and enumerable HashSet<long> construction; the corresponding
old negative probes are replaced by positive native evidence.

Integer CIL v33, object layout v23, object IR v34, interface plan v13 and native v61
record the admission/descriptor changes. Object fields and array buffer structures
are unchanged. All compiler/runtime resource limits remain unchanged.

## Validation and review

Thirteen probes provide 4,326 repeated O0/O2 native/.NET comparisons, including exact
64-bit payload checks, generic loads/stores, heap aliases across collection, empty
identity, type/interface tests, cast text, null/bounds faults, array interfaces,
List<long>, and both lazy/array HashSet<long> sources. All 13 initial checks pass.

A 90-check regression run had 89 passes and one obsolete Array.Empty<long>
negative. Its replacement, final fixture baseline and focused signed/unsigned
scalar-view checks now pass. All 105 selected distinct checks pass. The scalar
view test checks every bit through IList<ulong>/IReadOnlyList<ulong> over long[];
explicit unsigned enumeration/copy, UInt64 arrays and element borrows stay diagnostic.
Three mutations reject wrong array identity, wrong scalar width and missing fault
edges at both IR stages. An isolated model rejects the legacy profile and attached
nominal element ID. Raw native tests check zeroing, every stored bit, exact charge,
one-byte-short quotas, corrupt lengths/missing buffers, recovery, negative lengths
and empty-cache retention at both optimization levels. Adjacent suites cover
narrow/value vectors, array interfaces/mutation/copy/enumeration and empty caches.

Review checked exact primitive identities, generic helper packet/result widths,
array profile guards, null/bounds/ownership order, buffer charging and roots,
unsigned carrier operations without C++ signed-overflow assumptions, and all
unchanged descriptor kinds. Owned core source and resource limits are unchanged.
Native runs use forced GC, ASan/UBSan, 2,048-byte frames and unchanged 30-second
subprocess caps. The final public baseline remains 42 native controls/five gaps.
Local main 48ecf14 remains an ancestor. Full engine/replay is still unfinished.

Evidence: artifacts/alchemy-wide-vectors/ac7a-2{a,c,d,e}-tests.log and hashes.json.
Core: 8f67b4d565a228942e1878055386137b380ee5418054ac022b55979037c2e088.
Manifest: 6eafa64bb253a839b4c9942c211034812988013c086e640b05098b662b038cef.
Fixture: ea50f3540b9a3fd3f6dc2c5eede0aad725e6c54995c8a126db072254e06d5f90.
