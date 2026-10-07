# ADR AC0107: Dictionary capacity construction

Recorded 2026-10-07 for AC3.16.

## Decision

After committed AC7a.3 (edf44fc), the unchanged 1,024-type/2,048-method/262,144-work
engine diagnostic passes ZombieSiege's constant array initializer and reaches
Dictionary<VictoryEvaluatorId,IVictoryEvaluator>(int capacity) in
VictoryEvaluators.Register IL_0003. It charges 145,546 metadata units; StateLookup
remains eight methods/9,917 units. No engine emission or execution is attempted.
Evidence is archived in artifacts/alchemy-static-array-{refresh,engine}.

Add the default-comparer capacity constructor using the pinned Dictionary
constructor/Initialize behavior. Negative input throws ArgumentOutOfRangeException
with parameter capacity and the ordinary default message. Zero stays unallocated;
positive input selects the existing prime capacity. Allocate buckets and entries
into rooted locals, then publish both fields and the free-list sentinel. Share this
helper with the existing lazy first insertion. No compiler intrinsic, runtime
change, descriptor or resource-bound change is required. Comparer and other
constructors, broader key semantics and allocation-count equivalence remain outside
this slice.

## Validation and review

Six probes provide 3,276 repeated native/.NET comparisons across 13 capacities,
including prime thresholds, and seven payload/mode inputs. They cover empty maps,
growth, removal/reuse, clearing, Int64 values, traced reference values, collision
chains, negative-capacity message/parameter names and cursor invalidation/current.
The named collision key supplies its own ToString, keeping duplicate-key error
formatting within the existing admitted contract. Initial tests exposed that fixture
boundary; no production semantics were relaxed.

O0/O2 native quota tests induce ten construction failures across five heap limits,
verify frames/borrows/roots are released, collect partially allocated objects and
successfully construct again in the same runtime. Forced GC, ASan/UBSan, 2,048-byte
frames and 30-second subprocess caps stay enabled. The final 36-check run passes;
three additional dictionary library/manifest checks passed in the initial run.
All 39 selected distinct checks pass. The public baseline remains 43 controls/four
gaps. Six frozen inputs and 63 upstream originals match their pins.

Review checked negative/zero/positive ordering, prime selection, publication only
after both allocations, traced temporary lifetime, unchanged key callbacks and
free-list/version behavior. Local main 48ecf14 remains an ancestor. Profiles and
production limits are unchanged. Full engine compilation/execution and AC9
saved-game replay are unfinished.

Evidence: artifacts/alchemy-dictionary-capacity/tests-{a,b}.log and hashes.json.
Core: 34674ec186c932503323b4971bf3035468c1e09319388050494bfc4a5b6a0978.
Manifest: 427c07c2b86b41cd28e840e04407641d9b551ba0aaa44b74779f3c906cd7f275.
Fixture: cd97adfaea25f2c5ddc705d56cc68f5e08fc9bc903c96e6b6e9335307f3e0c74.
