# ADR AC0201: Bounded Stack storage

Recorded 2026-10-08 for AC3.34 after AC6.23.

Frozen ForcedMovementPlanner.Plan requires Stack<UnitId> to walk collision components.
Adapt pinned System.Collections Stack.cs parameterless constructor, Count, Push and
Pop. Preserve default empty storage, growth from four slots through doubling with the
pinned Array.MaxLength clamp, top-first removal and exact empty-stack exception text.
Managed allocation/copy publishes replacement storage only after success. Pop clears
reference-bearing slots, including embedded references; clearing scalar slots as well
is unobservable through this admitted surface. Enumerator versioning is deferred with
enumeration. Preserve IEnumerable<T>, ICollection and IReadOnlyCollection<T> nominal
facts; implement Count/synchronization identity and diagnose unimplemented traversal/
copy using the existing unsupported-member marker. Other constructors/members remain
unimplemented. No compiler/profile/limit/grant changes; CIL92/IR59/native79.

Validation: 23 selected checks pass in 27.621s. Eight probes cover 49 pairs, three
repetitions and O0/O2: 2352 new native/.NET comparisons for integer/wide/tuple/reference/
reference-containing inline values, growth, reuse, null/alias values, empty exceptions
and interface counts. Three admission controls reject traversal, copy and capacity
construction. Two native lifetime/quota tests prove popped references are collected:
10 live objects initially, 9 after releasing a popped result, 4 with empty retained
stack and 2 runtime roots after release. Growth failure at an object quota preserves
Count 8, restoring the quota permits Count 9, and complete draining/release returns to
those exact roots. Two existing residual-throw-stack checks also pass. Native tests
retain forced collection, ASan/UBSan, exact frame cleanup/exhaustion, 2048-byte frame
budget and 30-second subprocess limits.

Review checked unsigned Pop bounds before mutation, growth allocation/copy/publish
order, inline/reference rooting and removal, unsupported interface preservation and
quota recovery. All 90 upstream hashes and six frozen inputs verify. Isolated snapshot
38717eb; local main 48ecf14 remains an ancestor. No game changes or input rebuilding.

Evidence: artifacts/alchemy-stack{,-refresh,-engine}. Frozen engine discovery now
reports netstandard2.1 token 0x020009a0 CollectionExtensions, independently confirmed
by a metadata-only reader. ForcedMovementPlanner.Plan 0x06000cc8 IL_042b calls the
three-argument GetValueOrDefault<UnitId,int>. Engine charges 1270070 metadata units/
4954ms; StateLookup translates 15 methods/11616 units/461ms. Existing diagnostic-only
allowances remain unchanged; no engine emission/execution.
Core 709efb64d24294bedf48a188eff0f00bf09eb313820c4b97fe933c5ae0546801.
Manifest 8c6d606de348339e0e37f653061eafc910ab6daa8928c9dee89162de4e48357f.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
