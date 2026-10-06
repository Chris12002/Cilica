# ADR AC0046: Predicate first-element queries

Accepted 2026-10-07 for bounded AC6.5.

Implement predicate Enumerable.First and FirstOrDefault through one ordinary managed
search helper. Validate source before predicate even for empty inputs; return the
loaded element when its callback matches, preserving identity and mutations. Arrays
use an indexed traversal; reference enumerators retain foreach disposal on success,
exhaustion and exceptions. First throws the pinned no-matching-element message only
when search fails. A match containing zero or null remains a successful result.

The private found output uses Int32 because Boolean by-reference carriers remain
unsupported. This does not widen public compiler semantics or need an intrinsic.
Predicate First on Where enumerates and disposes the query, unlike the query-specific
nonpredicate optimization; the latter belongs in a separate slice. Nonpredicate
First/FirstOrDefault, List span specializations, boxed collection factories and
value-payload predicates remain diagnostic.

Eleven authored probes pass 3,234 repeated O0/O2 native/.NET comparisons on macOS
ARM64 with ASan/UBSan, forced collection, strict cleanup and native frame bounds.
Checks cover null order, empty/no-match messages, first match, source mutation,
reference/covariant arrays, matching default values, ignored collection Count,
callback/current/disposal faults and Where enumerator identity after consumption.
Thirty adjacent predicate/Where/materialization tests pass. The two unsupported
shape checks pass. Initial authored RVA-array and private Boolean-byref failures,
corrected runs, and the unmatched test filter are preserved in private evidence.

Review checked loaded-value ownership across predicate allocation, early return
through finally, disposal replacing an earlier fault, and successful default values.
The pinned First.cs and SHA-256 are recorded in source provenance. All compiler,
layout and native profiles remain unchanged.

Frozen Alchemy inputs are unchanged. Corrected-core discovery reaches unsupported
Func<GameState,PlayerId,bool> in PrepareForfeit IL_0395 (CIL2001); state discovery
retains 257/256 identities (CIL2003). See artifacts/alchemy-first-predicate-final.
No complete engine execution or saved-game replay is claimed.
