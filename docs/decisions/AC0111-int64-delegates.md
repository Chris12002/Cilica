# ADR AC0111: Exact Int64 delegate parameters and results

Recorded 2026-10-07 for AC2.6.

## Decision

After d47ef93, frozen engine discovery clears BoardTopology.Blocking and reaches
Func<UnitState, Int64> in InitiativeSlots.For IL_0033 after 175,695 metadata units.
The isolated 1024-type/2048-method/262144-work diagnostic bounds are unchanged.
StateLookup remains eight methods/9,919 units. No engine emission/execution.
Evidence: artifacts/alchemy-reference-array-{refresh,engine}; initiative-for-il.txt
also shows OrderBy with Int64 keys and explicit ordinal string ThenBy.

Admit exact Int64 parameters/results in the integer-object delegate profile.
Authentication compares signed scalar declaration and target identities before
lowering; same-width UInt64 substitutions remain rejected. Existing wide argument
packets and frame storage carry parameters. The private target table gains a
separate Int64 return function pointer and calls bit-cast its signed result back
to the existing unsigned 64-bit representation. No function-pointer cast, boxing
fallback, public ABI extension or new runtime allocation is needed.

Logical/scalar IR validates exact declaration, target and invocation signatures.
Receiver roots, argument roots, exception propagation and cleanup retain existing
rules. Unsigned/narrow/enum delegate signatures, byrefs, variance conversions,
multicast and hidden generic delegates remain outside this slice.

Integer CIL v37, delegate IR v8, scalar delegate IR v6, object IR v37, layout schema
v24 and native v64 record the capability. The schema admits the new result kind;
managed delegate payload and parameter packet layouts are unchanged. No library,
intrinsic grant or production capacity change.

## Validation and review

Eight probes provide 2,352 repeated O0/O2 native/.NET comparisons for signed/high
bits, two wide operands, captured receiver cycles, void/reference results, reference
arguments, closed generic delegates, arithmetic faults and null invocation/target
construction. Three mutation tests reject wrong result/argument widths and missing
fault edges at both logical/scalar stages. Two metadata mutations reject long/ulong
target substitutions before lowering. Former unsupported long fixtures now exercise
unsigned boundaries; the new signed probes cover the admitted behavior.

All 89 selected checks pass: 43 new/affected checks, then 46 native safety,
reference/value result and public-baseline regressions. ASan/UBSan, forced GC,
2,048-byte native stack limits and 30-second subprocess caps remain enabled.
Baseline: 43 native controls/four gaps. All six frozen input hashes still match.
Review checked width preservation, signed target authentication, typed function
pointers, result bit casts, receiver lifetime, fault propagation and older-profile
gating. No outstanding finding. Local main 48ecf14 remains an ancestor.

Full engine compilation and saved-game replay remain unfinished.
Evidence: artifacts/alchemy-wide-delegates/tests-{a,b}.log and hashes.json.
Core: d77f711997696db4e72d687ba835aed4eccd83fbd606ddfbae60911e3447b569.
Manifest: f6bae85816d585c77201a36ef4190bbef73e75d1cc75073eb789830fd49ecb48.
Fixture: 8378d59e06fe4a8d0b1e680de3654b03f05fd8c6915801491653c043e249db83.
