# ADR AC0177: Indexed Select

Recorded 2026-10-08 for AC6.20.

The unchanged ContinueShieldContribution calls indexed Select to construct choice
options. Add the exact two-generic overload. Validate source then selector eagerly;
empty arrays return the canonical result array. Other sources use a separate owned
IndexedSelectIterator adapting the pinned Select.cs yield algorithm. Reuse the
established indexed Where lifecycle rather than nonindexed Select optimization
interfaces. Source Current precedes checked index increment and selector execution.
Every terminal enumerates normally, retaining selector effects and source version
checks. No selector fusion or private framework layout is introduced.

Finally-based fault cleanup preserves unwind ordering and replacement disposal
faults. Terminate before user disposal, retain Current after completion/disposal,
and match the installed .NET 10.0.11 explicit-disposal reset/reuse behavior. Source
cursor roots are cleared after successful cleanup. The oracle MoveNext/Dispose IL
is archived; this does not promise every framework version's private layout.

Eleven probes supply 3,630 repeated O0/O2 native/.NET comparisons covering eager
validation, empty identity, lazy acquisition, indices/callbacks, clones/reuse,
source/selector/disposal faults, mutation, terminals, references, inline values,
different source/result types and boxed nongeneric Current. Two additional native
checks measure exact live roots before/after disposal and after query release.
All 24 focused and 39 adjacent/public-baseline checks pass (63 distinct checks),
with ASan/UBSan, forced GC, exact frame limits and cleanup. The old indexed missing-
overload assertion now checks successful binding. All 68 pinned hashes verify.

Review checked index/order, null acquisition, source disposal, retained Current,
reuse states, generic-result rooting and the absence of unintended fast paths.
Profiles, grants and production/diagnostic limits are unchanged. Main 48ecf14
remains an ancestor. Evidence: artifacts/alchemy-indexed-select{,-refresh,-engine}.

Frozen discovery advances to List<PendingOccurrence>.Reverse() at
ContinueShieldWave IL_1414: 964,532 charged metadata units in 4,094 ms. StateLookup
remains 15 methods/11,529 units (462 ms). Exact caller IL is archived. Discovery
counts vary with traversal and do not establish completion percentage. Full engine,
unchanged fingerprint and saved-game replay execution remain unfinished.
