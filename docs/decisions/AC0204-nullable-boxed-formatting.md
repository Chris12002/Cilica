# ADR AC0204: Format actual nullable box payloads

Recorded 2026-10-08 for AC7b.24 after AC1a.2.

Nullable<T> boxing produces null or a box of T, never a Nullable<T> box. The previous
formatting candidate scan selected the wrapper and then rejected the managed boxing
helper result during translation. Select the underlying named/enum payload after exact
nullable identity, access, closed-argument and constraint checks. Reuse the existing
formatter/renderer declaration authentication and grants. The managed nullable box
helper remains unchanged.

Discovery records the validated runtime payload type for each nullable CIL box. The
translator retains this fact only for the produced value in its current basic block;
formatting arguments must match that fact or the existing exact BoxValue witness.
Loading a local, a join, an arbitrary call or a direct helper call cannot manufacture
this proof. Candidate scanning alone never grants admission. The renderer unboxes the
actual payload, so mutable callbacks affect the retained box rather than the caller's
nullable storage. Primitive nullable formatting remains separately unsupported.

The shared managed formatter now skips payload dispatch for null arguments and applies
alignment to empty text, matching pinned arg?.ToString behavior. The first native run
caught this missing null guard: 16/54 failed; present mutable payloads and existing
controls passed. After the concrete guard fix, all 64 selected checks pass in 162.307s.
Eight new probes x 49 pairs x 3 repeats x O0/O2 plus the promoted nullable composite probe
provide 2646 new native/.NET comparisons. Coverage includes absent/present named and enum
payloads, null callback results, repeated mutation on one box, original-copy isolation,
exceptions, malformed/null format text, generic payloads, two/three mixed arguments and
five explicit alias/join/custom/primitive negatives. Existing boxed/mixed formatting,
private renderer/grant checks and nullable storage/boxing/quota tests pass. Native tests
use ASan/UBSan, forced GC, exact cleanup, frame exhaustion and bounded subprocesses.

Review checked actual payload identity, exact box provenance and block boundaries,
null/format parsing order, box retention, exception cleanup and unbox location ownership.
CIL93; IR59/native79, runtime implementation, grants and budgets unchanged. Core source
changes only the managed formatter null guard. All 92 upstream hashes and six frozen
inputs verify. Snapshot 91980d1; local main 48ecf14 remains an ancestor; no game changes.

Evidence: artifacts/alchemy-nullable-format{,-refresh,-engine}. Frozen discovery still
stops at Enumerable.Range in ConstructionContributions.EligiblePlayers 0x06000318 IL_0101.
Engine charges 1274169 metadata units/4915ms; StateLookup translates 15 methods/11616
units/458ms. Existing diagnostic allowances unchanged; no engine emission/execution.
Core cfa67fe9b93b039f248859bb59046e95e1639694e5899259d3a5677ffae3768c.
Manifest 5cb12a4d8b002eb2e6683257a657c5eeea483656b0dbce116f84d4718366b81e.
Full engine, unchanged fingerprint and saved-game replay remain unfinished.
