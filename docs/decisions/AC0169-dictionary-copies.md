# ADR AC0169: Generic dictionary copies

Recorded 2026-10-08 for AC3.29.

The frozen SettleHandLimits path builds a List from Dictionary.KeyCollection.
Implement KeyCollection.CopyTo, ValueCollection.CopyTo and generic
ICollection<KeyValuePair<TKey,TValue>>.CopyTo together using pinned Dictionary.cs.
Validate null array, index outside [0,length], then remaining capacity in that
order. Preserve index/array parameter names and exact messages. Capture the entry
count and backing vector, scan active entries in physical slot order and skip free
slots. Key/value copies use existing typed stores; pair copies construct independent
value pairs. No hash/equality calls, cursor allocation or version mutation occurs.
Covariant reference stores can leave earlier output elements changed before throwing.

A shared assembly-visible array-validation helper serves all three bodies. Nested
views use the existing count accessor and an assembly-visible vector accessor;
private access from a nested generic owner remains outside the compiler's current
proof boundary. This follows existing owned-core cursor bridges without relaxing
application access checks or changing game code. Nongeneric copies remain unsupported.
Profiles, grants and all resource limits remain unchanged.

Thirteen probes plus exact capacity cases supply 4,146 repeated native/.NET
comparisons at O0/O2 with forced GC, ASan/UBSan, frame budgets and cleanup checks.
They cover growth/removal/free-slot reuse, key/value/pair order, null/index/capacity
fault order, partial covariant copies, shared references, reference-bearing inline
values, empty/cleared dictionaries and List snapshots. Armed hash/equality callbacks
prove copying does not invoke them. Test insertion uses TryAdd for polymorphic and
unformatted struct keys, respecting the separately known Add error-text boundary.

Initial translation runs identified the private generic access boundary and those
two test insertion boundaries; all 13 targeted checks pass after using supported
owned helpers and insertion paths. Review checked validation ordering, captured
storage rooting, no callback/version changes, pair value copies and precise negative
scope. All 32 distinct selected checks pass, including existing dictionary
interface checks and the 46-control public baseline with one retained gap.
Evidence: artifacts/alchemy-dictionary-copy{,-refresh,-engine}.

Frozen engine discovery next reaches HashSet<T>.CopyTo through List.AddRange in
EffectTags.Combine. It charges 1,149,539 metadata units in 4,450 ms; StateLookup
translates 15 methods at 11,529 units (449 ms). Existing diagnostic capacities and
60-second command budgets remain unchanged. No full engine, unchanged fingerprint
or saved-game replay execution is claimed.
