# ADR AC0205: Bounded Int32 ranges

Recorded 2026-10-08 for AC6.24 after AC7b.24.

Frozen engine discovery reached Enumerable.Range in ConstructionContributions.
EligiblePlayers, token 0x06000318 IL_0101. Implement the exact Int32 factory and owned
iterator using ordinary managed operations. Pinned Range.cs and Range.SpeedOpt.cs
from dotnet/runtime 79d0c463f1b55624c874a11585f7e47731e8d675 are retained under MIT;
concrete arithmetic and managed fill loops replace generic numeric operators,
spans and SIMD. This adds no compiler intrinsic, privilege or native implementation.

Validate count and the last element using Int64 before allocation; zero count returns
the cached empty array. A nonempty range stores Int32 start and exclusive end, whose
unchecked wrap at Int32.MaxValue is intentional. Enumeration preserves pinned cursor
reuse, cloning, exhaustion and Dispose behavior, including retained Current. Read-only
IList/IReadOnlyList views preserve indexing, Contains, IndexOf, mutation rejection and
CopyTo bounds faults. Existing query interfaces support count, Any, First, Contains,
materialization and direct Skip. Select terminals use independent source traversal so
callbacks cannot advance or dispose a retained source cursor. Projected Skip and
unimplemented Enumerable members retain their existing diagnostics.

The initial wildcard run selected RangeTests plus ActualRangeTests/ListAddRangeTests:
45 of 47 passed in 139.670s. The two Copy cases failed in the .NET oracle because the
fixture hashed a null ParamName. Inspecting the pinned Span array constructor confirmed
its parameterless ArgumentOutOfRangeException; correct the managed CopyTo fault and
make the oracle hash null explicitly. All 24 exact RangeTests then pass in 85.960s.
Together with the 23 adjacent passing controls, this covers 47 distinct checks. Twelve
probes x49 input pairs x3 repeats xO0/O2 supply 3,528 new native/.NET comparisons.
Tests cover integer extremes, invalid/empty/large counts, multiple cursors, retained
Current, read-only lists, copy failure names/messages, terminal and selector callback
order/faults, retained source cursors, direct Skip and empty-array cache lifetime.
Native execution uses ASan/UBSan, forced collection, exact cleanup/root accounting,
frame exhaustion and bounded subprocesses. No full-suite claim is made.

Review checked long validation before construction, explicit wraparound, index/copy
bounds before writes, read-only views, source-cursor independence, callback order,
root lifetimes and propagation of allocation faults through existing managed calls.
All 94 retained upstream hashes and six frozen game inputs verify. Snapshot 8a5760e;
local main 48ecf14 remains an ancestor. CIL93/IR59/native79 and production limits stay
unchanged. No game-source changes or frozen assembly rebuilds.

Evidence: artifacts/alchemy-range{,-refresh,-engine}. Full diagnostic engine discovery
now stops at 8,194/8,192 aggregate locals (14,176/16,384 parameters), in List.Grow reached
through MovementResolutionHandler and PhysicalDamagePayload. It charges 1,307,877
metadata units in 4,929ms. StateLookup still translates 15 methods, 11,616 units/464ms.
Diagnostic allowances remain the existing recorded values; no engine emission or
execution occurred. Core 1990b6d0fe1aca3782d53847e3ecaaa85686b48bdc55613af9a4f15cc1779a01.
Manifest 77ba59321e73120d933d62a4e60cb3a2d64d2bd6326bc5efd549e82316d65664.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
