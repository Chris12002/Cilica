# ADR AC0125: Diagnostic parameter and metadata capacity

Recorded 2026-10-07 for diagnostic-only AC8.33.

After committed AC8.32 (a81576a), the 1,024-type/4,096-method diagnostic copy
reaches aggregate parameter/local storage at SortOrdinal through EffectTags.Normalize.
A counter-only probe measures 4,097 parameters against 4,096 and 2,422 locals
against 4,096 after 260,385 metadata units. Evidence: artifacts/alchemy-engine-
storage-audit. No additional limit changed in this measurement.

Raise only aggregate diagnostic parameters to 8,192. The copy then exhausts
262,144 metadata units at Dictionary.Insert through ShieldActivationPolicies.
Evidence: artifacts/alchemy-engine-parameters8192. Local storage remains 4,096.

Raise diagnostic metadata to 524,288, retaining the category counters. Discovery
reaches the internal struct EffectBindingContext as a Func generic argument in
EffectOperationRegistry::.cctor IL_0046 after 262,566 units in 1,674 ms. StateLookup
remains eight methods/9,921 units. Evidence: artifacts/alchemy-engine-metadata524288.
The budgets were extended only after observing their specific counters exhaust.

Review confirms only isolated source copies changed. Production still has 256
types, 512 methods, 65,536 metadata units and 4,096 aggregate parameters/locals.
Other diagnostic bounds remain unchanged. Each run archives its commands, base
commit, exact copied-source hashes and counter output. None emits or executes
native engine code. Complete closure capacity and saved-game replay remain open.
