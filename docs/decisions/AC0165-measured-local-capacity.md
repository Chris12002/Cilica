# ADR AC0165: Measured diagnostic local capacity

Recorded 2026-10-08 for AC8.50.

Committed 5090e50 advances frozen discovery through dictionary interface factories
but exhausts aggregate locals: 4,115/4,096, with 7,549/8,192 parameters. The diagnostic
counter charges 1,116,157 metadata units in 3,603 ms. Evidence:
artifacts/alchemy-dictionary-view-interfaces-engine/work1572864.json.

After that measured exhaustion, a fresh diagnostic copy changes MaximumLocals to
6,144. Other copied bounds remain types 2,048, methods 6,144, metadata 1,572,864,
instructions 131,072 and parameters 8,192. Each command is still capped at 60 seconds;
cheap caller/phase counters remain. Production source, profiles and all production
limits are unchanged. This remains discovery/translation only, with no engine
C++ emission or execution and no altered native frame/subprocess bounds.

The larger copy reaches Boolean.TryParse(String, Boolean&) at
ContributeShieldCardPrimitive.RequireValidBonds IL_0022 after 1,127,785 charged audit
units in 3,843 ms. StateLookup remains 15 methods/11,527 units. The exact frozen IL
requires a Boolean out location; do not infer another TryParse overload from its
name. Evidence: artifacts/alchemy-locals-6144/{run.py,audit.json,source-hashes.json,
work6144.json,require-bonds-il.txt,result.log}. Current core/manifest identities are
recorded in artifacts/alchemy-dictionary-view-interfaces/hashes.json.

Review confirms the capacity change is confined to archived copied source; source
assemblies and rule behavior are unchanged. The AC4.11 public baseline remains valid
(33 selected checks including four baseline checks); native tests are not rerun for
this documentation-only slice. Full engine, unchanged fingerprint execution and
saved-game replay remain unverified.
