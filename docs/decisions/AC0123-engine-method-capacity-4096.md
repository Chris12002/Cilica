# ADR AC0123: Third engine method-capacity diagnostic

Recorded 2026-10-07 for diagnostic-only AC8.31.

After committed AC7b.15 (0f3bad7), the isolated 1,024-type/262,144-metadata
copy reaches 2,048/2,048 methods in WhereIterator.Filter through InitiativeSlots.For.
It has read 127,095 IL bytes and 51,687 instructions, charging 249,770 metadata
units. Production remains 256 types, 512 methods and 65,536 metadata units.

Raise only the diagnostic copy's method bound to 4,096. Discovery now reaches
EnvironmentEffectSource::.ctor IL_000d calling the private-protected static base
helper ActiveEffectSource.RequireText. It charges 258,578 metadata units in
1,695 ms. StateLookup still translates eight methods/9,921 units. Neither harness
emits or executes native engine code; all other diagnostic limits are unchanged.

Review confirmed source-copy-only changes and unchanged production bounds. All
six frozen probe/application input hashes match their audit pins. Commands, base
commit and exact copied-source hashes are archived in artifacts/alchemy-engine-
capacity4096. The preceding 2,048-method refresh is in artifacts/alchemy-default-
integer-text-engine. Full engine capacity and AC9 saved-game replay remain open.
