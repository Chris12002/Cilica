# ADR AC0150: Reuse assignment rules inside interface planning

Recorded 2026-10-07 for AC8.43.

## Measurement and decision

After f5fb608, the frozen engine clears the OfTypeIterator variance blocker and
exhausts its copied 524,288 metadata budget in ReleaseFinished -> ToArray. The
aggregate recorded charge is 524,337 across ledgers. StateLookup still admits
15 methods after 11,511 units; production remains bounded by 256 discovered types.

Extending only copied metadata capacity to 786,432 hits the existing 60-second
probe cap. Removing per-charge stack traces in favor of compiler-supplied caller
names also times out, after a progress marker beyond 720,000 units. Neither
partial run proves closure admission. Both failures and copied source hashes
are retained under artifacts/alchemy-metadata-786432{,-light}.

Stop increasing limits. A timed 600,000-unit probe reads the first 2,997 methods
in 1,289 ms, builds their catalog by 1,771 ms, then spends about 40 seconds in
interface preparation before reporting a constraint metadata-budget diagnostic.
The planner constructs the entire reference assignment graph for every mapped
method and reconstructs boxed relationships for every interface declaration.
These structures depend only on the same immutable catalog for the current pass.

Reuse class signature rules, lazily built boxed relations and boxed signature
rules within Prepare. The boxed detached validator similarly reuses its signature
rules for one plan. Retain the existing distinct location capability settings;
new catalogs and validator instances receive new rules. No process-wide cache,
metadata authentication cache or skipped validation is introduced. Metadata
charges, graph work bounds, method/target limits and all dispatch choices remain
unchanged. No profile bump or native/runtime change is needed.

## Validation and review

All 152 targeted interface, variance, generic, value, boxed, initializer, enum-array
and malformed-plan checks pass. The before/after frozen 600,000-unit probes have
identical diagnostics, results and every metadata-work bucket (600,007 total
engine charge). Elapsed engine time is 41,913 versus 2,607 ms, about 16.1x faster
for this particular probe. StateLookup remains 15 methods/11,511 units. Evidence:
artifacts/alchemy-discovery-phase-audit and alchemy-discovery-reuse-600000,
including the exact uncommitted source patch, copied-source hashes and comparison.

Review checked cache scope, immutable catalog lifetime, fresh preparation after
closure growth, class versus boxed location capabilities, unchanged membership
and signature checks, metadata charging and native ownership. The optimization
has no managed behavior or generated native-code change. Main remains ancestor
48ecf14. The public baseline from AC8.42 is retained; no costly full-baseline rerun
was needed after this targeted refactor. Frozen inputs are unchanged.

The copied 786,432 metadata probe may now be retried with the same 60-second cap
because the measured repeated-graph bottleneck is fixed. Production stays at
256 types, 512 methods, 65,536 metadata/instruction units and 4,096 parameters/
locals. Other diagnostic limits stay at 2,048 types, 4,096 methods, 131,072
instructions, 8,192 parameters and 4,096 locals. No native engine emission or
execution, unchanged fingerprint execution or saved-game replay is established.
