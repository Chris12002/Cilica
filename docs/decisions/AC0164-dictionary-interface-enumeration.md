# ADR AC0164: Ordinary dictionary and view interface cursors

Recorded 2026-10-08 for AC4.11.

## Evidence and decision

After Cast, the frozen engine reaches Dictionary.KeyCollection's generic factory
from EffectSchedule.Group IL_0013, through EffectiveSpellCompiler. The diagnostic
copy charges 1,115,457 metadata units in 3,635 ms; StateLookup remains 15 methods
and 11,527 units. Evidence: artifacts/alchemy-linq-cast-engine/work1572864.json.
The reduced Order closure first reaches the dictionary's own unsupported nongeneric
factory: conservative interface discovery includes the allocated dictionary alongside
its views. Support ordinary pair enumeration as part of the same coherent family.

Use the pinned Dictionary.cs factories: nonempty generic calls box the existing
struct cursors, and nongeneric factories forward to their generic counterparts.
Nongeneric Current tests index zero or dictionary's live slot count plus one before
boxing the copied current value. Reference payloads retain identity; value payloads
produce fresh boxes. Existing version/slot scanning, Reset and no-op Dispose stay
unchanged, including allowed remove/clear/overwrite operations and rejected additions.

The pinned empty paths differ. Key/value views share SZGenericArrayEnumerator<T>.
Dictionary pairs use GenericEmptyEnumerator<KeyValuePair<TKey,TValue>>, a distinct
stateless singleton: MoveNext is always false, Reset/Dispose do nothing and Current
always reports not started. Adapt the already archived Array.Enumerators.cs contract
as a separate owned class with a private lazy per-type cache, traced by existing
instance static roots. Private framework iterator names/layouts are not advertised.

Do not partially implement IDictionary's separate DictionaryEntry mode. Its factory
and Entry/Key/Value accessors remain unsupported, including access through an ordinary
pair cursor cast to IDictionaryEnumerator. Pair/view copying and other dictionary
interfaces remain outside this slice. No compiler profile, grant, native layout,
production/diagnostic resource capacity or timeout changes are needed.

## Validation and review

Fourteen probes cover generic/nongeneric order, removed-slot reuse, boxed identity,
copy independence, default cursors, Current before/after traversal, Reset/Dispose,
all dictionary mutation modes, references after clearing, reference-bearing values,
nullable boxing, live clear-state faults and both empty cursor families. Explicit
pairs exercise all eight mutation modes; two negative checks retain legacy entry
API diagnostics. Concrete dictionary/view regressions retain their graph-liveness
checks. O0/O2, ASan/UBSan, forced collection, exact static-root cleanup, 2,048-byte
frames and 30-second native subprocess caps remain active.

The first native run exposes two differences in reduced assumptions: nongeneric
Current after Clear can fault even while generic Current retains a copied value,
and dictionary-pair empty cursors are not array cursors. The reference probe now
checks identity before Clear and generic retention afterward; separate ClearCurrent
checks the fault. Implement the distinct stateless pair cache and compare its
identity/state with .NET. Evidence: artifacts/alchemy-dictionary-view-interfaces/tests.log.

Review checks exact upstream factory choices, index checks before dictionary access
for default cursors, scalar/value/reference boxing, live count/version behavior,
rooting of boxed current fields and separate traced empty caches. Existing allocator,
collector and interface-dispatch semantics are unchanged. Full engine, unchanged
fingerprint execution and saved-game replay remain unverified.

All 29 selected dictionary/interface checks pass in 99.125 seconds after review.
The new slice contributes 4,116 repeated O0/O2 native/.NET comparisons from 14 probes
plus 96 comparisons across all eight mutation modes, for 4,212 total. Concrete
pair/view graph-liveness tests also pass. Evidence:
artifacts/alchemy-dictionary-view-interfaces/reviewed-tests.log.

All four public baseline checks pass in 69.365 seconds: 46 native controls and one
expected diagnostic gap. Total validation is 33 distinct checks. Main 48ecf14 remains
an ancestor. Evidence: artifacts/alchemy-dictionary-view-interfaces/baseline.log.
