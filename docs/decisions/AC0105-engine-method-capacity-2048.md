# ADR AC0105: Second engine method-capacity diagnostic

Recorded 2026-10-07 for diagnostic-only AC8.28.

After committed AC7a.2 (bdee3e2), the isolated 1,024-type/262,144-metadata
copy reaches 1,024/1,024 methods in AttackDeclaration.CanImpact IL_0012.
It has read 66,842 IL bytes, 25,969 instructions and charged 140,265 metadata
units. Production remains 256 types, 512 methods and 65,536 metadata units.

Raise only the diagnostic copy's method bound to 2,048. Discovery now reaches
ZombieSiege's static constant Int32 array initialization: ldtoken field-RVA data
followed by RuntimeHelpers.InitializeArray at IL_0028. The unmapped helper is
reached through Step, FinalizeAccepted, AdvanceRound and Evaluate. The probe
charges 141,987 metadata units in 1,020 ms. StateLookup still translates eight
methods with 9,917 units. Neither harness emits or executes native engine code.

Review confirmed source-copy-only limit changes and unchanged production bounds.
Input commands, base commit and exact copied-source hashes are archived in
artifacts/alchemy-engine-methods-2048. The preceding 1,024-method refresh is in
artifacts/alchemy-wide-vectors-engine. The capacity required by the complete
engine remains unknown; AC9 saved-game replay remains unfinished.
