# ADR AC0092: Captured list query materialization

Accepted 2026-10-07 for AC6.13.

Pinned Where/Select materializers use CollectionsMarshal.AsSpan for List sources.
Capture the backing array and logical count once in an owned managed helper,
including the pinned unsigned count/storage corruption guard. Fresh Where and
prefix cursors retain that array across allocations and source-list growth.
Propagate materialization terminal 3 through composed selectors; ordinary cursor
and other terminal semantics remain unchanged. No public Span API or compiler,
native profile, helper grant or resource-budget change is required.

Five new probes pass 798 repeated native/.NET comparisons at O0/O2: mutation during
Where, Select and composed queries; reference-bearing wide values and references;
source clearing, removal, insertion, growth and capacity replacement; callback
faults; original cursor preservation; and corrupted count/storage diagnostics.
Select.Where intentionally uses ordinary source enumeration and version checks.
Three old negative list cases now have 882 positive native comparisons.

The final selected set has 72 passing checks across b/c/d logs. The first broader
c run passed 48/52: two obsolete unsupported-list assertions and two clang O2
compilation timeouts in three-probe Where batches. Updated native cases and the
split batches pass all 16 checks in d (162.729 seconds). Every original Where input
is retained; no timeout or sanitizer setting was relaxed. Bounded native checks
retain forced collection, ASan/UBSan, exact quota recovery and cleanup, 2,048-byte
frame warnings and 30-second subprocess caps.

Review checked capture timing, array-prefix bounds, managed roots through callback
allocation, composed-selector traversal, independent cursor state, disposal and
unchanged ordinary enumeration. All 63 pinned-source hashes verify. Frozen engine
and state inputs remain unchanged and still hit the production 256-type bound;
ToList, other List terminal specializations and full saved-game replay remain open.

Evidence: artifacts/alchemy-list-materialization/ac6-13b-*, ac6-13c-*, ac6-13d-*,
engine-and-state.json and core/library-build.json.
Core: e0a79d5a640bb7dadbaca53624a4a343a428d3245e6e6d845997bb5b4ec2f199.
Manifest: 2c56a390d936b006af5de8b0e628b96a9fe774035c98859066e122731796aeed.
Fixture: 9e14a152e64b482230111625126dbe0a62d4d475cbdeb054c3319f175aae2cdf.
