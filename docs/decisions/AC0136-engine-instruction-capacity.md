# ADR AC0136: Diagnostic aggregate instruction capacity

Recorded 2026-10-07 for diagnostic-only AC8.36.

After AC3.23 (5087c01), frozen discovery exhausts the 65,536 aggregate instruction
budget. A counter-only copy reads 65,534 instructions across 2,777 method
declarations, 158,849 IL bytes, 5,346 parameters and 2,922 locals. The next body
exhausts its remaining two instructions. It charges 327,295 metadata work units.
Evidence: artifacts/alchemy-engine-instruction-audit. No limit changed in this run.

Raise only the isolated copy's aggregate instruction limit to 131,072. Discovery
then reaches Array.IndexOf<string> in EffectOperationDescriptor.Validate IL_0013
through EffectOperationDefinition.Validate IL_0025 after 327,656 metadata units
in 2,389 ms. StateLookup remains eight methods/9,925 units. Evidence:
artifacts/alchemy-engine-instructions131072, including commands, copied-source
hashes and verified hashes for all six frozen inputs. No native emission/execution.

Review confirms production limits remain unchanged: 256 types, 512 methods,
65,536 instructions/metadata work units and 4,096 parameters/locals. The diagnostic
copy keeps 1,024 types, 4,096 methods, 524,288 metadata units, 8,192 parameters and
4,096 locals. Per-method decoding and all other limits remain unchanged. Full
closure admission, unchanged fingerprint execution and saved-game replay remain open.
