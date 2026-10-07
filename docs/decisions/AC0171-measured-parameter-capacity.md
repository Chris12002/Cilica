# ADR AC0171: Measured diagnostic parameter capacity

Recorded 2026-10-08 for AC8.52.

AC3.30 HashSet copies advance frozen discovery to 8,193/8,192 aggregate parameters
with 4,504/6,144 locals and 1,159,312 charged metadata units. After recording that
exhaustion, raise only the disposable compiler copy's MaximumParameters to 12,288.
Other diagnostic bounds stay types 2048, methods/locals 6144, instructions 131072
and metadata 1572864. Every command remains capped at 60 seconds. Production limits,
profiles, native frame/subprocess bounds and frozen inputs remain unchanged.

The copy next reports the unsupported OrdinalCaseSensitiveComparer.GetHashCode
body while discovering TypeKeyOperations.Hash through Dictionary<Type,...> in
CreateHandlers. Charged work is 1,159,620 units in 4,155 ms. StateLookup remains 15
methods/11,529 units (472 ms). Exact owned IL shows TypeKeyOperations.Hash loads
Type& using ldind.ref then calls Object.GetHashCode virtually. This may be excessive
virtual reachability rather than a needed comparer operation; resolve that before
implementing another hash API. Both comparer overload bodies and the type-key IL
are archived for follow-up.

Review confirms the capacity change is confined to copied compiler source; cheap
caller/phase counters, no engine emission/execution, and all original safety/time
limits remain. Evidence: artifacts/alchemy-parameters-12288/{run.py,audit.json,
source-hashes.json,work12288.json,type-key-hash-il.txt,ordinal-comparer-hash-il.txt}.
Core/manifest identities are in artifacts/alchemy-hashset-copy/hashes.json. The 35
AC3.30 selected checks remain the validation baseline; no semantic tests were rerun
for this evidence-only slice. Full engine/fingerprint/replay execution is unfinished.
