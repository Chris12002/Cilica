# ADR AC0159: Further measured diagnostic metadata capacity

Recorded 2026-10-08 for AC8.47.

Committed 13f3bdf advances frozen engine discovery through the HashCode mapping
but exhausts the copied 1,048,576-unit metadata bound at ReferenceKeyOperations.Equal.
The aggregate audit counter records 1,048,623 charged units in 3,673 ms (it also
counts auxiliary root budgets; this is not the remaining-budget field).
Evidence: artifacts/alchemy-hashcode-pair-engine.

After the measured exhaustion, a fresh diagnostic-only compiler copy raises
metadata to 1,572,864. Other copied bounds remain types 2,048, methods 6,144,
instructions 131,072, parameters 8,192 and locals 4,096. The command cap remains
60 seconds. Production source, profiles and all production limits are unchanged.

The larger copy reaches static-field access rejection at
AlchemyGameEngine/AlchemyResolutionAdapter.Prepare IL_02ed after 1,100,871 audit
units in 3,695 ms. StateLookup remains 15 methods/11,517 units. Evidence:
artifacts/alchemy-metadata-1572864/{run.py,audit.json,source-hashes.json,work1572864.json,probe.log}.
No engine C++ is emitted or executed. The current public baseline remains the
AC5a.6 validation; no native tests were repeated for this documentation-only slice.

Review confirms the capacity change is confined to archived copied source, the
unchanged frozen inputs and the existing cheap caller/phase instrumentation.
Full engine, unchanged fingerprint execution and saved-game replay remain unverified.
