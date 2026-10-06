# ADR AC0055: Predicate Single and SingleOrDefault

Accepted 2026-10-07 for AC6.9.

The frozen ZombieSiege.AdvanceRound closure requires predicate Single. Add the
exact two-argument Single and SingleOrDefault contracts as ordinary owned managed
code. Share a private TryGetSingle helper, using an Int32 found flag as in
predicate First. No new compiler intrinsic, profile or resource limit is needed.

Preserve the pinned .NET 10.0.11 algorithm: validate source before predicate,
load each candidate before invoking its predicate, retain the first match, and
scan the remaining elements until completion or a second match. A later callback
may mutate the source, allocate or throw. Return the first loaded value without
reloading it. The array path uses direct indexing instead of a private span.
Fallback enumeration uses one cursor and always performs its using cleanup.
Single reports no match only after helper disposal; a throwing Dispose can
therefore replace success, no-match, predicate failure or a second-match failure.
Both match-count messages match the pinned resources exactly.

This slice admits arrays and supported reference-class enumeration, including
Where/OfType and authored yield cursors. Existing copy/return and root lowering
handles named inline values, nullable values and managed references. It does not
implement List span access, boxed collection factories, parameterless Single or
SingleOrDefault, or explicit-default overloads. Those contracts stay diagnostic.

Sixteen probes pass 4,704 repeated native/.NET comparisons at O0/O2 with ASan/UBSan,
forced collection, fixed frame limits and exact cleanup. Coverage includes null
order, zero/one/multiple matches, matched zero/null/default structs, covariant
arrays, mutations to earlier/later array entries, reference/inline-value roots,
nullable results, yield cursors, collection-count avoidance, query cursor state,
and get/move/Current/predicate/disposal failures before and after a first match.
Three negative fixtures pin unsupported parameterless and boxed List contracts.
The first test run passed every native probe but expected different diagnostic
wording in two negative checks. Corrected checks plus the added late-fault probe,
adjacent First and public baseline pass: 43 tests in the final run.

Review checked loop bounds, single reads of Current, retained loaded values,
rooted result slots, cleanup precedence, exact mapping boundaries and unchanged
budgets. The shared helper follows the pinned algorithm without compiler changes.
Logs and frozen evidence are under artifacts/alchemy-single. Owned core SHA256:
ccb5b124686d6d0e494ae976fb6686d997f4ca2841bf91ab04cb4fe00de69977.
Manifest SHA256:
b97294e36a70e1464ef6ef99841e22108e4df43c91f3b4a050bf96980ef8664c.

Frozen engine discovery advances to CIL2003, Type classification metadata limit
exceeded, in FinalizeAccepted IL_0165. StateLookup remains 257/256 discovered
identities. Audit metadata-only classification versus actual layout discovery
before considering capacity. Full native engine execution and saved-game replay
remain unverified. Public baseline stays at 31 native controls and 16 gaps.
